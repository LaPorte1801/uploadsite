namespace UploadSite.Web.ViewModels.Admin;

public sealed class AdminIndexViewModel
{
    public CreateUserViewModel CreateUser { get; init; } = new();
    public IReadOnlyList<UserListItemViewModel> Users { get; init; } = [];
}
