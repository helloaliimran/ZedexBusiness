using System.ComponentModel.DataAnnotations;

namespace Zedex.Api.DTOs;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}
