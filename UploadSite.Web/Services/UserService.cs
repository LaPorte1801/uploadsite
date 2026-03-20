using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using UploadSite.Web.Data;
using UploadSite.Web.Enums;
using UploadSite.Web.Models;

namespace UploadSite.Web.Services;

public sealed class UserService(AppDbContext dbContext) : IUserService
{
    public async Task<AppUser?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalizedUserName = userName.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            x => x.UserName.ToLower() == normalizedUserName,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken)
    {
        var normalizedUserName = userName.Trim().ToLowerInvariant();
        return dbContext.Users.AnyAsync(x => x.UserName.ToLower() == normalizedUserName, cancellationToken);
    }

    public async Task<AppUser> CreateUserAsync(string userName, string password, UserRole role, CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            UserName = userName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<IReadOnlyList<AppUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .OrderByDescending(x => x.Role)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (id == currentUserId)
        {
            return false;
        }

        var user = await dbContext.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return false;
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
