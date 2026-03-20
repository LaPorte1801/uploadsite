using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UploadSite.Web.Enums;
using UploadSite.Web.Services;
using UploadSite.Web.ViewModels.Admin;

namespace UploadSite.Web.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminController(
    IUserService userService,
    CurrentUserAccessor currentUserAccessor) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await userService.GetUsersAsync(cancellationToken);
        var model = new AdminIndexViewModel
        {
            Users = users.Select(x => new UserListItemViewModel
            {
                Id = x.Id,
                UserName = x.UserName,
                Role = x.Role,
                CreatedAtUtc = x.CreatedAtUtc
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Index(cancellationToken);
        }

        if (await userService.UserNameExistsAsync(model.UserName, cancellationToken))
        {
            TempData["AdminError"] = "A user with this username already exists.";
            return RedirectToAction(nameof(Index));
        }

        await userService.CreateUserAsync(model.UserName, model.Password, model.Role, cancellationToken);
        TempData["AdminSuccess"] = $"User '{model.UserName}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserAccessor.UserId;
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var deleted = await userService.DeleteUserAsync(id, currentUserId.Value, cancellationToken);
        TempData[deleted ? "AdminSuccess" : "AdminError"] = deleted
            ? "User deleted."
            : "Could not delete the selected user.";

        return RedirectToAction(nameof(Index));
    }
}
