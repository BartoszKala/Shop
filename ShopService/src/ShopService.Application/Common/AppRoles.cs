namespace ShopService.Application.Common;

/// <summary>Role nadawane przez IdentityService (claim <c>role</c>) – nazwy muszą być z nim zgodne.</summary>
public static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";
}

/// <summary>Claimy tokenu JWT wydawanego przez IdentityService.</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
