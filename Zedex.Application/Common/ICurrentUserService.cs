namespace Zedex.Application.Common;

/// <summary>Abstracts the signed-in user for audit stamping.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
}
