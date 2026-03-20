using UploadSite.Web.Enums;

namespace UploadSite.Web.ViewModels.Admin;

public sealed class UserListItemViewModel
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
