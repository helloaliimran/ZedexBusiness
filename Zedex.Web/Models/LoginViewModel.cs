using System.ComponentModel.DataAnnotations;

namespace Zedex.Web.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username or Email")]
    public string UserName { get; set; } = default!;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = default!;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
