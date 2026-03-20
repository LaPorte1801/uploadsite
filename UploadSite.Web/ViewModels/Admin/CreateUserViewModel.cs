using System.ComponentModel.DataAnnotations;
using UploadSite.Web.Enums;

namespace UploadSite.Web.ViewModels.Admin;

public sealed class CreateUserViewModel
{
    [Required]
    [StringLength(128, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.User;
}
