namespace UploadSite.Web.ViewModels.Upload;

public sealed class UploadDashboardViewModel
{
    public UploadFormViewModel Form { get; init; } = new();
    public IReadOnlyList<UploadHistoryItemViewModel> RecentItems { get; init; } = [];
}
