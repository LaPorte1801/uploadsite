using UploadSite.Web.Enums;

namespace UploadSite.Web.Models;

public sealed class ImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string StagingRelativePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public int Year { get; set; }
    public int TrackNumber { get; set; }
    public bool HasEmbeddedCover { get; set; }
    public bool CoverCopiedToAlbumFolder { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Uploaded;
    public string? ValidationSummary { get; set; }
    public string? DuplicateSummary { get; set; }
    public string? TargetRelativePath { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public DateTimeOffset? ImportedAtUtc { get; set; }

    public AppUser UploadedByUser { get; set; } = null!;
}
