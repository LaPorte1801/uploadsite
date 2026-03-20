using UploadSite.Web.Enums;

namespace UploadSite.Web.ViewModels.Upload;

public sealed class UploadHistoryItemViewModel
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int TrackNumber { get; init; }
    public int Year { get; init; }
    public ImportStatus Status { get; init; }
    public string? ValidationSummary { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
}
