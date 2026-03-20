using System.Security.Claims;
using UploadSite.Web.Enums;

namespace UploadSite.Web.Services;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public string? UserName => httpContextAccessor.HttpContext?.User.Identity?.Name;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role), out var role)
            ? role
            : null;
}
