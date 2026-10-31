using IdentityService.Application.Abstractions;
using IdentityService.Application.Contracts;
using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

internal sealed class UserRoleRepository(AppIdentityDbContext db) : IUserRoleRepository
{
    public async Task<IReadOnlyList<string>> GetRolesAsync(UserId userId, CancellationToken ct = default) =>
        await db.UserRoles
            .Where(r => r.UserId == userId)
            .Select(r => r.Role)
            .OrderBy(r => r)
            .ToListAsync(ct);

    public async Task<Result> AddToRoleAsync(UserId userId, string role, CancellationToken ct = default)
    {
        var userRole = UserRole.Create(userId, role);
        if (!userRole.IsSuccess)
        {
            return userRole.Error;
        }

        if (!await db.UserRoles.AnyAsync(r => r.UserId == userId && r.Role == role, ct))
        {
            db.UserRoles.Add(userRole.Value);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result> SetRolesAsync(UserId userId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var created = roles.Select(role => UserRole.Create(userId, role)).ToList();
        if (created.FirstOrDefault(r => !r.IsSuccess) is { } failed)
        {
            return failed.Error!;
        }

        var current = await db.UserRoles.Where(r => r.UserId == userId).ToListAsync(ct);
        db.UserRoles.RemoveRange(current.Where(r => !roles.Contains(r.Role)));
        db.UserRoles.AddRange(created.Select(r => r.Value).Where(r => current.All(c => c.Role != r.Role)));
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

internal sealed class ConsentRepository(AppIdentityDbContext db) : IConsentRepository
{
    public async Task AddAsync(UserConsent consent, CancellationToken ct = default)
    {
        db.UserConsents.Add(consent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserConsent>> GetHistoryAsync(UserId userId, CancellationToken ct = default) =>
        await db.UserConsents
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.RecordedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct);
}

internal sealed class SecurityEventLog(AppIdentityDbContext db, IRequestContext request, TimeProvider timeProvider)
    : ISecurityEventLog
{
    public async Task RecordAsync(SecurityEventType type, UserId? userId, string? details = null, CancellationToken ct = default)
    {
        var actor = request.UserId is { } caller && caller != userId ? caller : (UserId?)null;
        db.SecurityEvents.Add(SecurityEvent.Create(
            type, userId, actor, timeProvider.GetUtcNow(), request.IpAddress, request.UserAgent, details));
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResponse<SecurityEventResponse>> GetForUserAsync(UserId userId, PageRequest page, CancellationToken ct = default)
    {
        var query = db.SecurityEvents.AsNoTracking().Where(e => e.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(e => new SecurityEventResponse(
                e.Id, e.Type, e.OccurredAt, e.IpAddress, e.UserAgent, e.Details, e.ActorId != null))
            .ToListAsync(ct);

        return new PagedResponse<SecurityEventResponse>(items, page.Page, page.PageSize, total);
    }
}

internal sealed class AdminUserQueries(AppIdentityDbContext db, TimeProvider timeProvider) : IAdminUserQueries
{
    public async Task<PagedResponse<AdminUserListItem>> SearchAsync(string? search, PageRequest page, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Email!, pattern)
                || EF.Functions.ILike(u.FirstName!, pattern)
                || EF.Functions.ILike(u.LastName!, pattern));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmailConfirmed,
                IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now,
                Roles = u.Roles.Select(r => r.Role).OrderBy(r => r).ToList(),
                u.CreatedAt,
                u.LastLoginAt
            })
            .ToListAsync(ct);

        var items = rows
            .Select(u => new AdminUserListItem(
                u.Id,
                u.Email!,
                $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } name ? name : null,
                u.EmailConfirmed,
                u.IsLockedOut,
                u.Roles,
                u.CreatedAt,
                u.LastLoginAt))
            .ToList();

        return new PagedResponse<AdminUserListItem>(items, page.Page, page.PageSize, total);
    }
}

internal sealed class UnitOfWork(AppIdentityDbContext db) : IUnitOfWork
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default)
        where TResult : Result
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action();
                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(ct);
                }
                else
                {
                    await transaction.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                }

                return result;
            }
            catch
            {
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
