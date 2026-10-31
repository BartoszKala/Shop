using System.ComponentModel.DataAnnotations;
using IdentityService.Domain;

namespace IdentityService.Application.Contracts;

public sealed record AdminUserListItem(
    UserId Id,
    string Email,
    string? FullName,
    bool EmailConfirmed,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record AdminUserDetailsResponse(
    UserId Id,
    string Email,
    string? FirstName,
    string? LastName,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool HasPassword,
    DateTimeOffset? LockedOutUntil,
    int AccessFailedCount,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<SessionResponse> ActiveSessions);

public sealed record SearchUsersRequest
{
    [MaxLength(256)]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, PageRequest.MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public PageRequest ToPageRequest() => new() { Page = Page, PageSize = PageSize };
}

public sealed record SetRolesRequest
{
    [Required]
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed record LockUserRequest
{
    public DateTimeOffset? Until { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}
