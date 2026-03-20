using System.ComponentModel.DataAnnotations;

namespace UploadSite.Web.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
