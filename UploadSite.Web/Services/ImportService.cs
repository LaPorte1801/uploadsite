using Microsoft.EntityFrameworkCore;
using TagLibByteVector = TagLib.ByteVector;
using TagLibFile = TagLib.File;
using TagLibPicture = TagLib.Picture;
using TagLibPictureType = TagLib.PictureType;
using UploadSite.Web.Data;
using UploadSite.Web.Enums;
using UploadSite.Web.Models;
using UploadSite.Web.ViewModels.Upload.Batch;

namespace UploadSite.Web.Services;

public sealed class ImportService(
    AppDbContext dbContext,
    AppPaths appPaths,
    IAudioMetadataService audioMetadataService)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".m4a",
        ".flac"
    };
    private const int MaxCoverSizeBytes = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<ImportJob>> CreateImportsAsync(Guid userId, IEnumerable<IFormFile> files, CancellationToken cancellationToken)
    {
        var createdImports = new List<ImportJob>();

        foreach (var file in files)
        {
            createdImports.Add(await CreateImportAsync(userId, file, Guid.NewGuid(), cancellationToken));
        }

        await SplitCreatedImportsIntoReviewBatchesAsync(createdImports, cancellationToken);

        foreach (var import in createdImports)
        {
            await dbContext.Entry(import).ReloadAsync(cancellationToken);
        }

        return createdImports;
    }

    public async Task<ImportJob> CreateImportAsync(Guid userId, IFormFile file, CancellationToken cancellationToken)
    {
        return await CreateImportAsync(userId, file, Guid.NewGuid(), cancellationToken);
    }

    private async Task<ImportJob> CreateImportAsync(Guid userId, IFormFile file, Guid batchId, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only MP3, M4A, and FLAC files are supported.");
        }

        var importId = Guid.NewGuid();
        var storedFileName = $"{importId}{extension.ToLowerInvariant()}";
        var stagingRelativePath = Path.Combine(importId.ToString("N"), storedFileName);
        var absolutePath = Path.Combine(appPaths.StagingRoot, stagingRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var stream = File.Create(absolutePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var validation = audioMetadataService.Validate(absolutePath, file.FileName);
        var evaluation = await EvaluateAsync(
            validation.Artist,
            validation.AlbumArtist,
            validation.Album,
            validation.Title,
            validation.Genre,
            validation.Year,
            validation.TrackNumber,
            validation.HasEmbeddedCover,
            extension,
            cancellationToken);

        var import = new ImportJob
        {
            Id = importId,
            BatchId = batchId,
            UploadedByUserId = userId,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            StagingRelativePath = stagingRelativePath,
            ContentType = file.ContentType,
            Artist = validation.Artist,
            AlbumArtist = validation.AlbumArtist,
            Album = validation.Album,
            Title = validation.Title,
            Genre = validation.Genre,
            Year = validation.Year,
            TrackNumber = validation.TrackNumber,
            HasEmbeddedCover = validation.HasEmbeddedCover,
            Status = !validation.RequiresManualReview && evaluation.DuplicateCandidates.Count == 0 ? ImportStatus.ReadyToImport : ImportStatus.NeedsReview,
            ValidationSummary = BuildInitialValidationSummary(validation, evaluation.ValidationSummary),
            DuplicateSummary = BuildDuplicateSummary(evaluation.DuplicateCandidates),
            TargetRelativePath = evaluation.ProposedRelativePath
        };

        dbContext.ImportJobs.Add(import);
        await dbContext.SaveChangesAsync(cancellationToken);
        return import;
    }

    public async Task<IReadOnlyList<ImportJob>> GetDashboardItemsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await dbContext.ImportJobs
            .Include(x => x.UploadedByUser)
            .Where(x => x.UploadedByUserId == userId)
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(x => x.UploadedAtUtc)
            .Take(20)
            .ToList();
    }

    public async Task<IReadOnlyList<ImportJob>> GetReviewQueueAsync(CancellationToken cancellationToken)
    {
        var items = await dbContext.ImportJobs
            .Include(x => x.UploadedByUser)
            .Where(x => x.Status == ImportStatus.NeedsReview || x.Status == ImportStatus.ReadyToImport)
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(x => x.UploadedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<BatchReviewGroupListItemViewModel>> GetReviewGroupsAsync(CancellationToken cancellationToken)
    {
        var items = await GetReviewQueueAsync(cancellationToken);

        return items
            .GroupBy(x => x.BatchId)
            .Select(group => new BatchReviewGroupListItemViewModel
            {
                BatchId = group.Key,
                AlbumArtist = group.OrderByDescending(x => x.UploadedAtUtc).First().AlbumArtist,
                Album = group.OrderByDescending(x => x.UploadedAtUtc).First().Album,
                Year = group.OrderByDescending(x => x.UploadedAtUtc).First().Year,
                TrackCount = group.Count(),
                ReadyCount = group.Count(x => x.Status == ImportStatus.ReadyToImport),
                HasAnyEmbeddedCover = group.Any(x => x.HasEmbeddedCover),
                UploadedBy = group.OrderByDescending(x => x.UploadedAtUtc).First().UploadedByUser.UserName,
                LatestUploadAtUtc = group.Max(x => x.UploadedAtUtc)
            })
            .OrderByDescending(x => x.LatestUploadAtUtc)
            .ToList();
    }

    public async Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ImportJobs
            .Include(x => x.UploadedByUser)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<BatchReviewViewModel?> GetBatchReviewAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var imports = await GetBatchImportsAsync(batchId, cancellationToken);
        if (imports.Count == 0)
        {
            return null;
        }

        var first = imports[0];
        var duplicateCandidates = new List<BatchDuplicateCandidateViewModel>();

        foreach (var import in imports)
        {
            var evaluation = await EvaluateAsync(
                import.Artist,
                import.AlbumArtist,
                import.Album,
                import.Title,
                import.Genre,
                import.Year,
                import.TrackNumber,
                import.HasEmbeddedCover,
                Path.GetExtension(import.StoredFileName),
                cancellationToken,
                import.Id);

            duplicateCandidates.AddRange(evaluation.DuplicateCandidates.Select(x => new BatchDuplicateCandidateViewModel
            {
                RelativePath = x.RelativePath,
                Reason = x.Reason
            }));
        }

        return new BatchReviewViewModel
        {
            BatchId = batchId,
            Artist = first.Artist,
            AlbumArtist = first.AlbumArtist,
            Album = first.Album,
            Year = first.Year,
            Genre = first.Genre,
            HasAnyEmbeddedCover = imports.Any(x => x.HasEmbeddedCover),
            CoverPreviewUrl = UrlSafeCoverPreviewRoute(batchId),
            DuplicateSummary = BuildDuplicateSummary(duplicateCandidates
                .GroupBy(x => $"{x.RelativePath}|{x.Reason}")
                .Select(x => new DuplicateCandidate { RelativePath = x.First().RelativePath, Reason = x.First().Reason })
                .ToList()),
            DuplicateCandidates = duplicateCandidates
                .GroupBy(x => $"{x.RelativePath}|{x.Reason}")
                .Select(x => x.First())
                .ToList(),
            Tracks = imports
                .OrderBy(x => x.TrackNumber)
                .ThenBy(x => x.Title)
                .Select(x => new BatchTrackEditItemViewModel
                {
                    Id = x.Id,
                    OriginalFileName = x.OriginalFileName,
                    Title = x.Title,
                    TrackNumber = x.TrackNumber,
                    ReadyToImport = x.Status == ImportStatus.ReadyToImport,
                    HasEmbeddedCover = x.HasEmbeddedCover,
                    ValidationSummary = x.ValidationSummary ?? string.Empty,
                    ProposedRelativePath = x.TargetRelativePath ?? string.Empty
                })
                .ToList()
        };
    }

    public async Task<bool> UpdateMetadataAsync(Guid id, EditImportRequest request, CancellationToken cancellationToken)
    {
        var import = await dbContext.ImportJobs.FindAsync([id], cancellationToken);
        if (import is null)
        {
            return false;
        }

        import.Artist = request.Artist.Trim();
        import.AlbumArtist = request.AlbumArtist.Trim();
        import.Album = request.Album.Trim();
        import.Title = request.Title.Trim();
        import.Genre = string.IsNullOrWhiteSpace(request.Genre) ? null : request.Genre.Trim();
        import.Year = request.Year;
        import.TrackNumber = request.TrackNumber;
        import.ReviewedAtUtc = DateTimeOffset.UtcNow;

        await ApplyMetadataToStagingFileAsync(import, request.CoverImage, cancellationToken);

        var evaluation = await EvaluateAsync(
            import.Artist,
            import.AlbumArtist,
            import.Album,
            import.Title,
            import.Genre,
            import.Year,
            import.TrackNumber,
            import.HasEmbeddedCover,
            Path.GetExtension(import.StoredFileName),
            cancellationToken,
            import.Id);

        import.TargetRelativePath = evaluation.ProposedRelativePath;
        import.ValidationSummary = evaluation.ValidationSummary;
        import.DuplicateSummary = BuildDuplicateSummary(evaluation.DuplicateCandidates);
        import.Status = request.ReadyToImport && evaluation.MetadataComplete && evaluation.DuplicateCandidates.Count == 0
            ? ImportStatus.ReadyToImport
            : ImportStatus.NeedsReview;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateBatchAsync(BatchUpdateRequest request, CancellationToken cancellationToken)
    {
        var trackIds = request.Tracks.Select(x => x.Id).ToHashSet();
        var imports = await dbContext.ImportJobs
            .Where(x => x.BatchId == request.BatchId && trackIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (imports.Count == 0)
        {
            return false;
        }

        foreach (var import in imports)
        {
            var trackRequest = request.Tracks.Single(x => x.Id == import.Id);
            import.Artist = request.Artist.Trim();
            import.AlbumArtist = request.AlbumArtist.Trim();
            import.Album = request.Album.Trim();
            import.Genre = string.IsNullOrWhiteSpace(request.Genre) ? null : request.Genre.Trim();
            import.Year = request.Year;
            import.Title = trackRequest.Title.Trim();
            import.TrackNumber = trackRequest.TrackNumber;
            import.ReviewedAtUtc = DateTimeOffset.UtcNow;

            await ApplyMetadataToStagingFileAsync(import, request.CoverImage, cancellationToken);

            var evaluation = await EvaluateAsync(
                import.Artist,
                import.AlbumArtist,
                import.Album,
                import.Title,
                import.Genre,
                import.Year,
                import.TrackNumber,
                import.HasEmbeddedCover,
                Path.GetExtension(import.StoredFileName),
                cancellationToken,
                import.Id);

            import.TargetRelativePath = evaluation.ProposedRelativePath;
            import.ValidationSummary = evaluation.ValidationSummary;
            import.DuplicateSummary = BuildDuplicateSummary(evaluation.DuplicateCandidates);
            import.Status = trackRequest.ReadyToImport && evaluation.MetadataComplete && evaluation.DuplicateCandidates.Count == 0
                ? ImportStatus.ReadyToImport
                : ImportStatus.NeedsReview;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ImportCommitResult> CommitImportAsync(Guid id, CancellationToken cancellationToken)
    {
        var import = await dbContext.ImportJobs.FindAsync([id], cancellationToken);
        if (import is null)
        {
            return ImportCommitResult.NotFound;
        }

        if (import.Status != ImportStatus.ReadyToImport)
        {
            return ImportCommitResult.NotReady;
        }

        var evaluation = await EvaluateAsync(
            import.Artist,
            import.AlbumArtist,
            import.Album,
            import.Title,
            import.Genre,
            import.Year,
            import.TrackNumber,
            import.HasEmbeddedCover,
            Path.GetExtension(import.StoredFileName),
            cancellationToken,
            import.Id);

        var targetDirectory = Path.Combine(appPaths.LibraryRoot, Path.GetDirectoryName(evaluation.ProposedRelativePath)!);
        var targetPath = Path.Combine(appPaths.LibraryRoot, evaluation.ProposedRelativePath);

        Directory.CreateDirectory(targetDirectory);

        if (!evaluation.MetadataComplete)
        {
            import.Status = ImportStatus.NeedsReview;
            import.ValidationSummary = evaluation.ValidationSummary;
            import.DuplicateSummary = BuildDuplicateSummary(evaluation.DuplicateCandidates);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ImportCommitResult.NotReady;
        }

        if (evaluation.DuplicateCandidates.Count > 0 || File.Exists(targetPath))
        {
            import.Status = ImportStatus.NeedsReview;
            import.ValidationSummary = evaluation.ValidationSummary;
            import.DuplicateSummary = BuildDuplicateSummary(evaluation.DuplicateCandidates);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ImportCommitResult.PotentialDuplicate;
        }

        var sourcePath = Path.Combine(appPaths.StagingRoot, import.StagingRelativePath);
        File.Move(sourcePath, targetPath, overwrite: false);

        using (var audioFile = TagLibFile.Create(targetPath))
        {
            var picture = audioFile.Tag.Pictures?.FirstOrDefault();
            if (picture is not null)
            {
                await File.WriteAllBytesAsync(
                    Path.Combine(targetDirectory, "cover.jpg"),
                    picture.Data.Data,
                    cancellationToken);

                import.CoverCopiedToAlbumFolder = true;
            }
        }

        import.TargetRelativePath = evaluation.ProposedRelativePath;
        import.Status = ImportStatus.Imported;
        import.ImportedAtUtc = DateTimeOffset.UtcNow;
        import.ValidationSummary = "Imported into the Jellyfin music library.";
        import.DuplicateSummary = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ImportCommitResult.Imported;
    }

    public async Task<BatchCommitResult> CommitBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var imports = await GetBatchImportsAsync(batchId, cancellationToken);
        if (imports.Count == 0)
        {
            return BatchCommitResult.NotFound;
        }

        var readyImports = imports.Where(x => x.Status == ImportStatus.ReadyToImport).OrderBy(x => x.TrackNumber).ToList();
        if (readyImports.Count == 0)
        {
            return BatchCommitResult.NothingReady;
        }

        foreach (var import in readyImports)
        {
            var result = await CommitImportAsync(import.Id, cancellationToken);
            if (result != ImportCommitResult.Imported)
            {
                return BatchCommitResult.PartialFailure;
            }
        }

        return BatchCommitResult.Imported;
    }

    public async Task<ImportEvaluationResult?> GetEvaluationAsync(Guid id, CancellationToken cancellationToken)
    {
        var import = await dbContext.ImportJobs.FindAsync([id], cancellationToken);
        if (import is null)
        {
            return null;
        }

        return await EvaluateAsync(
            import.Artist,
            import.AlbumArtist,
            import.Album,
            import.Title,
            import.Genre,
            import.Year,
            import.TrackNumber,
            import.HasEmbeddedCover,
            Path.GetExtension(import.StoredFileName),
            cancellationToken,
            import.Id);
    }

    public async Task<bool> RejectAsync(Guid id, CancellationToken cancellationToken)
    {
        var import = await dbContext.ImportJobs.FindAsync([id], cancellationToken);
        if (import is null)
        {
            return false;
        }

        import.Status = ImportStatus.Rejected;
        import.ReviewedAtUtc = DateTimeOffset.UtcNow;
        import.ValidationSummary = "Rejected during manual review.";
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var imports = await GetBatchImportsAsync(batchId, cancellationToken);
        if (imports.Count == 0)
        {
            return false;
        }

        foreach (var import in imports)
        {
            import.Status = ImportStatus.Rejected;
            import.ReviewedAtUtc = DateTimeOffset.UtcNow;
            import.ValidationSummary = "Rejected during batch review.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmbeddedCoverResult?> GetBatchEmbeddedCoverAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var import = (await GetBatchImportsAsync(batchId, cancellationToken))
            .FirstOrDefault(x => x.HasEmbeddedCover);

        return import is null ? null : await GetEmbeddedCoverAsync(import.Id, cancellationToken);
    }

    public async Task<EmbeddedCoverResult?> GetEmbeddedCoverAsync(Guid id, CancellationToken cancellationToken)
    {
        var import = await dbContext.ImportJobs.FindAsync([id], cancellationToken);
        if (import is null)
        {
            return null;
        }

        var sourcePath = Path.Combine(appPaths.StagingRoot, import.StagingRelativePath);
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        using var audioFile = TagLibFile.Create(sourcePath);
        var picture = audioFile.Tag.Pictures?.FirstOrDefault();
        if (picture is null)
        {
            return null;
        }

        return new EmbeddedCoverResult
        {
            ContentType = string.IsNullOrWhiteSpace(picture.MimeType) ? "image/jpeg" : picture.MimeType,
            Bytes = picture.Data.Data
        };
    }

    private async Task<ImportEvaluationResult> EvaluateAsync(
        string artist,
        string albumArtist,
        string album,
        string title,
        string? genre,
        int year,
        int trackNumber,
        bool hasEmbeddedCover,
        string extension,
        CancellationToken cancellationToken,
        Guid? importId = null)
    {
        var validationErrors = ValidateMetadata(
            artist,
            albumArtist,
            album,
            title,
            genre,
            year,
            trackNumber,
            hasEmbeddedCover);

        var proposedRelativePath = BuildTargetRelativePath(albumArtist, album, title, year, trackNumber, extension);
        var duplicateCandidates = await FindDuplicateCandidatesAsync(
            artist,
            albumArtist,
            album,
            title,
            year,
            trackNumber,
            proposedRelativePath,
            cancellationToken,
            importId);

        if (duplicateCandidates.Count > 0)
        {
            validationErrors.Add("Potential duplicate found in the existing library.");
        }

        return new ImportEvaluationResult
        {
            MetadataComplete = validationErrors.Count == 0 && duplicateCandidates.Count == 0,
            ValidationSummary = validationErrors.Count == 0 ? "Metadata looks complete." : string.Join(Environment.NewLine, validationErrors),
            ProposedRelativePath = proposedRelativePath,
            DuplicateCandidates = duplicateCandidates
        };
    }

    private async Task<IReadOnlyList<DuplicateCandidate>> FindDuplicateCandidatesAsync(
        string artist,
        string albumArtist,
        string album,
        string title,
        int year,
        int trackNumber,
        string proposedRelativePathWithoutExtension,
        CancellationToken cancellationToken,
        Guid? importId)
    {
        var candidates = new List<DuplicateCandidate>();
        var normalizedArtist = NormalizeForCompare(albumArtist);
        var normalizedAlbum = NormalizeForCompare(album);
        var normalizedTitle = NormalizeForCompare(title);
        var targetDirectory = Path.Combine(
            appPaths.LibraryRoot,
            FileNameSanitizer.Sanitize(albumArtist),
            FileNameSanitizer.Sanitize($"{year} - {album}"));

        if (Directory.Exists(targetDirectory))
        {
            foreach (var filePath in Directory.EnumerateFiles(targetDirectory)
                         .Where(x => AllowedExtensions.Contains(Path.GetExtension(x))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (fileName.StartsWith($"{trackNumber:00} - ", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(new DuplicateCandidate
                    {
                        RelativePath = Path.GetRelativePath(appPaths.LibraryRoot, filePath),
                        Reason = "Same album folder and track number already exist."
                    });
                }

                try
                {
                    using var file = TagLibFile.Create(filePath);
                    var tag = file.Tag;
                    if (NormalizeForCompare(tag.FirstAlbumArtist) == normalizedArtist &&
                        NormalizeForCompare(tag.Album) == normalizedAlbum &&
                        (NormalizeForCompare(tag.Title) == normalizedTitle || (int)tag.Track == trackNumber))
                    {
                        candidates.Add(new DuplicateCandidate
                        {
                            RelativePath = Path.GetRelativePath(appPaths.LibraryRoot, filePath),
                            Reason = "Metadata in the existing library matches this upload."
                        });
                    }
                }
                catch
                {
                    // Ignore files with unreadable tags in duplicate scan.
                }
            }
        }

        var pendingConflicts = await dbContext.ImportJobs
            .Where(x => x.Id != importId &&
                        (x.Status == ImportStatus.ReadyToImport || x.Status == ImportStatus.NeedsReview) &&
                        x.AlbumArtist == albumArtist &&
                        x.Album == album &&
                        (x.Title == title || x.TrackNumber == trackNumber))
            .Select(x => new DuplicateCandidate
            {
                RelativePath = x.TargetRelativePath ?? x.OriginalFileName,
                Reason = "Another pending upload has matching album metadata."
            })
            .ToListAsync(cancellationToken);

        candidates.AddRange(pendingConflicts);

        return candidates
            .DistinctBy(x => $"{x.RelativePath}|{x.Reason}")
            .Take(10)
            .ToList();
    }

    private string BuildTargetRelativePath(
        string albumArtist,
        string album,
        string title,
        int year,
        int trackNumber,
        string extension)
    {
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".mp3" : extension;
        var artistFolder = FileNameSanitizer.Sanitize(albumArtist);
        var albumFolder = FileNameSanitizer.Sanitize($"{year} - {album}");
        var fileName = FileNameSanitizer.Sanitize($"{trackNumber:00} - {title}{safeExtension}");
        return Path.Combine(artistFolder, albumFolder, fileName);
    }

    private static List<string> ValidateMetadata(
        string artist,
        string albumArtist,
        string album,
        string title,
        string? genre,
        int year,
        int trackNumber,
        bool hasEmbeddedCover)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(artist))
        {
            errors.Add("Artist is required.");
        }

        if (string.IsNullOrWhiteSpace(albumArtist))
        {
            errors.Add("Album Artist is required.");
        }

        if (string.IsNullOrWhiteSpace(album))
        {
            errors.Add("Album is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("Title is required.");
        }

        if (year <= 0)
        {
            errors.Add("Year is required.");
        }

        if (trackNumber <= 0)
        {
            errors.Add("Track Number is required.");
        }

        if (!hasEmbeddedCover)
        {
            errors.Add("Embedded cover art is required.");
        }

        return errors;
    }

    private static string? BuildDuplicateSummary(IReadOnlyList<DuplicateCandidate> duplicateCandidates)
    {
        if (duplicateCandidates.Count == 0)
        {
            return null;
        }

        return string.Join(
            Environment.NewLine,
            duplicateCandidates.Select(x => $"{x.Reason} [{x.RelativePath}]"));
    }

    private static string NormalizeForCompare(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string BuildInitialValidationSummary(AudioValidationResult validation, string evaluationSummary)
    {
        var messages = new List<string>();
        messages.AddRange(validation.Warnings);

        if (!string.IsNullOrWhiteSpace(evaluationSummary) && evaluationSummary != "Metadata looks complete.")
        {
            messages.Add(evaluationSummary);
        }

        return messages.Count == 0 ? evaluationSummary : string.Join(Environment.NewLine, messages);
    }

    private async Task SplitCreatedImportsIntoReviewBatchesAsync(IReadOnlyList<ImportJob> imports, CancellationToken cancellationToken)
    {
        if (imports.Count <= 1)
        {
            return;
        }

        foreach (var group in imports.GroupBy(BuildReviewBatchKey))
        {
            var batchId = Guid.NewGuid();
            foreach (var import in group)
            {
                import.BatchId = batchId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildReviewBatchKey(ImportJob import)
    {
        if (ShouldStayIsolated(import))
        {
            return $"single:{import.Id}";
        }

        return string.Join(
            "|",
            NormalizeBatchPart(import.AlbumArtist),
            NormalizeBatchPart(import.Album),
            import.Year.ToString());
    }

    private static bool ShouldStayIsolated(ImportJob import)
    {
        return string.IsNullOrWhiteSpace(import.AlbumArtist) ||
               string.IsNullOrWhiteSpace(import.Album) ||
               import.Year <= 0 ||
               string.Equals(import.AlbumArtist, "Unknown Artist", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(import.Album, "Unknown Album", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBatchPart(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private async Task ApplyMetadataToStagingFileAsync(ImportJob import, IFormFile? coverImage, CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(appPaths.StagingRoot, import.StagingRelativePath);
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException("The staging file could not be found.");
        }

        await using var coverStream = coverImage is null ? null : new MemoryStream();
        if (coverImage is not null)
        {
            if (coverImage.Length == 0)
            {
                throw new InvalidOperationException("The selected cover image is empty.");
            }

            if (coverImage.Length > MaxCoverSizeBytes)
            {
                throw new InvalidOperationException("The cover image is too large. Keep it under 10 MB.");
            }

            var extension = Path.GetExtension(coverImage.FileName);
            if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only JPEG cover images are supported right now.");
            }

            await coverImage.CopyToAsync(coverStream!, cancellationToken);
        }

        using var audioFile = TagLibFile.Create(sourcePath);
        var tag = audioFile.Tag;
        tag.Performers = [import.Artist];
        tag.AlbumArtists = [import.AlbumArtist];
        tag.Album = import.Album;
        tag.Title = import.Title;
        tag.Genres = string.IsNullOrWhiteSpace(import.Genre) ? [] : [import.Genre];
        tag.Year = (uint)Math.Max(0, import.Year);
        tag.Track = (uint)Math.Max(0, import.TrackNumber);

        if (coverStream is not null)
        {
            tag.Pictures =
            [
                new TagLibPicture(new TagLibByteVector(coverStream.ToArray()))
                {
                    Type = TagLibPictureType.FrontCover,
                    MimeType = "image/jpeg",
                    Description = "Front cover"
                }
            ];
        }

        audioFile.Save();
        import.HasEmbeddedCover = tag.Pictures?.Length > 0;
    }

    private async Task<List<ImportJob>> GetBatchImportsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        return await dbContext.ImportJobs
            .Include(x => x.UploadedByUser)
            .Where(x =>
                (x.Status == ImportStatus.NeedsReview || x.Status == ImportStatus.ReadyToImport) &&
                x.BatchId == batchId)
            .ToListAsync(cancellationToken);
    }

    private static string UrlSafeCoverPreviewRoute(Guid batchId)
    {
        return $"/Upload/BatchCover?batchId={batchId}";
    }
}

public sealed class EditImportRequest
{
    public string Artist { get; init; } = string.Empty;
    public string AlbumArtist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Genre { get; init; }
    public int Year { get; init; }
    public int TrackNumber { get; init; }
    public IFormFile? CoverImage { get; init; }
    public bool ReadyToImport { get; init; }
}

public sealed class BatchUpdateRequest
{
    public Guid BatchId { get; init; }
    public string Artist { get; init; } = string.Empty;
    public string AlbumArtist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public int Year { get; init; }
    public string? Genre { get; init; }
    public IFormFile? CoverImage { get; init; }
    public IReadOnlyList<BatchTrackUpdateRequest> Tracks { get; init; } = [];
}

public sealed class BatchTrackUpdateRequest
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public int TrackNumber { get; init; }
    public bool ReadyToImport { get; init; }
}

public enum ImportCommitResult
{
    NotFound = 0,
    NotReady = 1,
    PotentialDuplicate = 2,
    Imported = 3
}

public enum BatchCommitResult
{
    NotFound = 0,
    NothingReady = 1,
    PartialFailure = 2,
    Imported = 3
}

public sealed class EmbeddedCoverResult
{
    public string ContentType { get; init; } = "image/jpeg";
    public byte[] Bytes { get; init; } = [];
}
