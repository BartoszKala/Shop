using IdentityService.Api.Extensions;
using IdentityService.Application.Contracts;
using IdentityService.Application.Services;
using IdentityService.Domain;
using IdentityService.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/users")]
[Produces("application/json")]
public sealed class AdminUsersController(AdminUserService adminService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AdminUserListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminUserListItem>>> Search([FromQuery] SearchUsersRequest request, CancellationToken ct) =>
        this.ToActionResult(await adminService.SearchAsync(request, ct));

    [HttpGet("{userId}")]
    [ProducesResponseType<AdminUserDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailsResponse>> Get(UserId userId, CancellationToken ct) =>
        this.ToActionResult(await adminService.GetAsync(userId, ct));

    [HttpPut("{userId}/roles")]
    [ProducesResponseType<AdminUserDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserDetailsResponse>> SetRoles(UserId userId, SetRolesRequest request, CancellationToken ct) =>
        this.ToActionResult(await adminService.SetRolesAsync(userId, request, ct));

    [HttpPost("{userId}/lock")]
    [ProducesResponseType<AdminUserDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminUserDetailsResponse>> Lock(UserId userId, LockUserRequest request, CancellationToken ct) =>
        this.ToActionResult(await adminService.LockAsync(userId, request, ct));

    [HttpPost("{userId}/unlock")]
    [ProducesResponseType<AdminUserDetailsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUserDetailsResponse>> Unlock(UserId userId, CancellationToken ct) =>
        this.ToActionResult(await adminService.UnlockAsync(userId, ct));

    [HttpPost("{userId}/force-password-reset")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ForcePasswordReset(UserId userId, CancellationToken ct) =>
        this.ToActionResult(await adminService.ForcePasswordResetAsync(userId, ct));

    [HttpDelete("{userId}/sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RevokeSessions(UserId userId, CancellationToken ct) =>
        this.ToNoContent(await adminService.RevokeSessionsAsync(userId, ct));

    [HttpGet("{userId}/security-events")]
    [ProducesResponseType<PagedResponse<SecurityEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SecurityEventResponse>>> SecurityEvents(
        UserId userId, [FromQuery] PageRequest page, CancellationToken ct) =>
        this.ToActionResult(await adminService.GetSecurityEventsAsync(userId, page, ct));
}
