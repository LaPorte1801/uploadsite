using UploadSite.Web.Enums;
using UploadSite.Web.Models;

namespace UploadSite.Web.Services;

public interface IUserService
{
    Task<AppUser?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);
    Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken);
    Task<AppUser> CreateUserAsync(string userName, string password, UserRole role, CancellationToken cancellationToken);
    Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken);
    Task<bool> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken);
}
