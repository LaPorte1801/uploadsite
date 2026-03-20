using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UploadSite.Web.Enums;
using UploadSite.Web.Services;
using UploadSite.Web.ViewModels.Upload;
using UploadSite.Web.ViewModels.Upload.Batch;

namespace UploadSite.Web.Controllers;

[Authorize]
public sealed class UploadController(
    ImportService importService,
    CurrentUserAccessor currentUserAccessor) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        if (userId is null)
        {
            return Challenge();
        }

        var recentItems = await importService.GetDashboardItemsAsync(userId.Value, cancellationToken);
        var model = new UploadDashboardViewModel
        {
            RecentItems = recentItems.Select(MapHistoryItem).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(1024L * 1024L * 1024L)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1024L * 1024L * 1024L)]
    public async Task<IActionResult> Create(UploadFormViewModel model, CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.UserId;
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid || model.AudioFiles.Count == 0)
        {
            TempData["UploadError"] = "Select at least one MP3, M4A, or FLAC file before uploading.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var imports = await importService.CreateImportsAsync(userId.Value, model.AudioFiles, cancellationToken);
            var readyCount = imports.Count(x => x.Status == ImportStatus.ReadyToImport);
            var reviewCount = imports.Count - readyCount;
            TempData["UploadSuccess"] = $"Uploaded {imports.Count} file(s). Ready: {readyCount}. Needs review: {reviewCount}.";
        }
        catch (Exception ex)
        {
            TempData["UploadError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Review(CancellationToken cancellationToken)
    {
        var model = await importService.GetReviewGroupsAsync(cancellationToken);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> EditBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var model = await importService.GetBatchReviewAsync(batchId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> EditBatch(BatchReviewViewModel model, Guid batchId, CancellationToken cancellationToken)
    {
        model.BatchId = batchId;

        if (!ModelState.IsValid)
        {
            var currentModel = await importService.GetBatchReviewAsync(batchId, cancellationToken);
            if (currentModel is not null)
            {
                currentModel.Artist = model.Artist;
                currentModel.AlbumArtist = model.AlbumArtist;
                currentModel.Album = model.Album;
                currentModel.Year = model.Year;
                currentModel.Genre = model.Genre;

                foreach (var track in currentModel.Tracks)
                {
                    var postedTrack = model.Tracks.FirstOrDefault(x => x.Id == track.Id);
                    if (postedTrack is null)
                    {
                        continue;
                    }

                    track.Title = postedTrack.Title;
                    track.TrackNumber = postedTrack.TrackNumber;
                    track.ReadyToImport = postedTrack.ReadyToImport;
                }

                return View(currentModel);
            }

            return View(model);
        }

        var updated = await importService.UpdateBatchAsync(
            new BatchUpdateRequest
            {
                BatchId = batchId,
                Artist = model.Artist,
                AlbumArtist = model.AlbumArtist,
                Album = model.Album,
                Genre = model.Genre,
                Year = model.Year,
                CoverImage = model.CoverImage,
                Tracks = model.Tracks.Select(x => new BatchTrackUpdateRequest
                {
                    Id = x.Id,
                    Title = x.Title,
                    TrackNumber = x.TrackNumber,
                    ReadyToImport = x.ReadyToImport
                }).ToList()
            },
            cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        TempData["UploadSuccess"] = "Batch review saved.";
        return RedirectToAction(nameof(EditBatch), new { batchId });
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> BatchCover(Guid batchId, CancellationToken cancellationToken)
    {
        var cover = await importService.GetBatchEmbeddedCoverAsync(batchId, cancellationToken);
        if (cover is null)
        {
            return NotFound();
        }

        return File(cover.Bytes, cover.ContentType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> RejectBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var rejected = await importService.RejectBatchAsync(batchId, cancellationToken);
        TempData[rejected ? "UploadSuccess" : "UploadError"] = rejected
            ? "Album batch rejected and removed from the active queue."
            : "Batch not found.";

        return RedirectToAction(nameof(Review));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> CommitBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await importService.CommitBatchAsync(batchId, cancellationToken);
        TempData[result == BatchCommitResult.Imported ? "UploadSuccess" : "UploadError"] = result switch
        {
            BatchCommitResult.Imported => "Ready tracks were imported into the Jellyfin library.",
            BatchCommitResult.NothingReady => "No tracks in this batch are marked ready for import.",
            BatchCommitResult.PartialFailure => "Some tracks could not be imported. Review the batch again.",
            _ => "Batch not found."
        };

        return result == BatchCommitResult.Imported
            ? RedirectToAction(nameof(Review))
            : RedirectToAction(nameof(EditBatch), new { batchId });
    }

    private static UploadHistoryItemViewModel MapHistoryItem(Models.ImportJob job)
    {
        return new UploadHistoryItemViewModel
        {
            Id = job.Id,
            OriginalFileName = job.OriginalFileName,
            Artist = job.Artist,
            Album = job.Album,
            Title = job.Title,
            TrackNumber = job.TrackNumber,
            Year = job.Year,
            Status = job.Status,
            ValidationSummary = job.ValidationSummary,
            UploadedAtUtc = job.UploadedAtUtc
        };
    }
}
