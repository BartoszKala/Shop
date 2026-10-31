using System.ComponentModel.DataAnnotations;
using IdentityService.Domain;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Contracts;

public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record SecurityEventResponse(
    SecurityEventId Id,
    SecurityEventType Type,
    DateTimeOffset OccurredAt,
    string? IpAddress,
    string? UserAgent,
    string? Details,
    bool PerformedByAnotherUser);
