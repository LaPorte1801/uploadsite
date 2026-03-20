using System.ComponentModel.DataAnnotations;
using UploadSite.Web.Enums;

namespace UploadSite.Web.ViewModels.Upload;

public sealed class ReviewImportViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    public string UploadedBy { get; set; } = string.Empty;

    [Required]
    public ImportStatus Status { get; set; }

    [Required]
    public string Artist { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Album artist")]
    public string AlbumArtist { get; set; } = string.Empty;

    [Required]
    public string Album { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Genre { get; set; }

    [Range(1, 3000)]
    public int Year { get; set; }

    [Range(1, 999)]
    [Display(Name = "Track number")]
    public int TrackNumber { get; set; }

    public string? ValidationSummary { get; set; }
    public string? DuplicateSummary { get; set; }
    public string ProposedRelativePath { get; set; } = string.Empty;
    public bool MetadataComplete { get; set; }
    public bool HasEmbeddedCover { get; set; }
    public IReadOnlyList<DuplicateCandidateViewModel> DuplicateCandidates { get; set; } = [];
    public string? CoverPreviewUrl { get; set; }

    [Display(Name = "JPEG cover art")]
    public IFormFile? CoverImage { get; set; }

    [Display(Name = "Mark as ready to import")]
    public bool ReadyToImport { get; set; }
}

public sealed class DuplicateCandidateViewModel
{
    public string RelativePath { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
