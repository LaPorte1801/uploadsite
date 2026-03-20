using System.ComponentModel.DataAnnotations;

namespace UploadSite.Web.ViewModels.Upload.Batch;

public sealed class BatchReviewViewModel
{
    public Guid BatchId { get; set; }

    [Required]
    public string AlbumArtist { get; set; } = string.Empty;

    [Required]
    public string Artist { get; set; } = string.Empty;

    [Required]
    public string Album { get; set; } = string.Empty;

    [Range(1, 3000)]
    public int Year { get; set; }

    public string? Genre { get; set; }

    [Display(Name = "JPEG cover art for all tracks")]
    public IFormFile? CoverImage { get; set; }

    public string? CoverPreviewUrl { get; set; }
    public bool HasAnyEmbeddedCover { get; set; }
    public string? DuplicateSummary { get; set; }
    public List<BatchDuplicateCandidateViewModel> DuplicateCandidates { get; set; } = [];
    public List<BatchTrackEditItemViewModel> Tracks { get; set; } = [];
}

public sealed class BatchTrackEditItemViewModel
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public bool ReadyToImport { get; set; }
    public bool HasEmbeddedCover { get; set; }
    public string ValidationSummary { get; set; } = string.Empty;
    public string ProposedRelativePath { get; set; } = string.Empty;
}

public sealed class BatchDuplicateCandidateViewModel
{
    public string RelativePath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class BatchReviewGroupListItemViewModel
{
    public Guid BatchId { get; set; }
    public string AlbumArtist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public int Year { get; set; }
    public int TrackCount { get; set; }
    public int ReadyCount { get; set; }
    public bool HasAnyEmbeddedCover { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset LatestUploadAtUtc { get; set; }
}
