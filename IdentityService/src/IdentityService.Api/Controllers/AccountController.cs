using IdentityService.Api.Extensions;
using IdentityService.Application.Contracts;
using IdentityService.Application.Services;
using IdentityService.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
[Produces("application/json")]
public sealed class AccountController(AccountService accountService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken ct) =>
        this.ToActionResult(await accountService.GetProfileAsync(this.UserId(), ct));

    [HttpPut("me")]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        this.ToActionResult(await accountService.UpdateProfileAsync(this.UserId(), request, ct));

    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokensResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        this.ToActionResult(await accountService.ChangePasswordAsync(this.UserId(), request, ct));

    [HttpPost("change-email")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> ChangeEmail(ChangeEmailRequest request, CancellationToken ct) =>
        this.ToActionResult(await accountService.ChangeEmailAsync(this.UserId(), request, ct));

    [HttpPost("confirm-email-change")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> ConfirmEmailChange(ConfirmEmailChangeRequest request, CancellationToken ct) =>
        this.ToActionResult(await accountService.ConfirmEmailChangeAsync(request, ct));

    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyList<SessionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> Sessions(CancellationToken ct) =>
        this.ToActionResult(await accountService.GetSessionsAsync(this.UserId(), ct));

    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeSession(SessionId sessionId, CancellationToken ct) =>
        this.ToNoContent(await accountService.RevokeSessionAsync(this.UserId(), sessionId, ct));

    [HttpDelete("sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RevokeOtherSessions(CancellationToken ct) =>
        this.ToNoContent(await accountService.RevokeOtherSessionsAsync(this.UserId(), ct));

    [HttpGet("security-events")]
    [ProducesResponseType<PagedResponse<SecurityEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SecurityEventResponse>>> SecurityEvents(
        [FromQuery] PageRequest page, CancellationToken ct) =>
        this.ToActionResult(await accountService.GetSecurityEventsAsync(this.UserId(), page, ct));
}
