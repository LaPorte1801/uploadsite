namespace UploadSite.Web.Services;

public sealed class AudioValidationResult
{
    public bool IsValid => Errors.Count == 0 && Warnings.Count == 0;
    public bool RequiresManualReview => Errors.Count > 0 || Warnings.Count > 0;
    public bool HasFallbackArtist { get; set; }
    public bool HasFallbackAlbumArtist { get; set; }
    public bool HasFallbackAlbum { get; set; }
    public bool HasFallbackTitle { get; set; }
    public bool HasMissingYear { get; set; }
    public bool HasFallbackTrackNumber { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public string Artist { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public int Year { get; set; }
    public int TrackNumber { get; set; }
    public bool HasEmbeddedCover { get; set; }
}
