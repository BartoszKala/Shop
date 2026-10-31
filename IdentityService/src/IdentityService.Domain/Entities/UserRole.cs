using IdentityService.Domain.Common;
using IdentityService.Domain.Constants;

namespace IdentityService.Domain.Entities;

public class UserRole
{
    private UserRole()
    {
    }

    public UserId UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;

    public static Result<UserRole> Create(UserId userId, string role)
    {
        if (!AppRoles.All.Contains(role))
        {
            return Error.Validation(nameof(Role), $"Nieznana rola '{role}'.");
        }

        return new UserRole { UserId = userId, Role = role };
    }
}
