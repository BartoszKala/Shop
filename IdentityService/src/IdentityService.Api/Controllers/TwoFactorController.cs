using IdentityService.Api.Extensions;
using IdentityService.Application.Contracts;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/2fa")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public sealed class TwoFactorController(TwoFactorService twoFactorService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<TwoFactorStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TwoFactorStatusResponse>> Status() =>
        this.ToActionResult(await twoFactorService.GetStatusAsync(this.UserId()));

    [HttpPost("authenticator")]
    [ProducesResponseType<AuthenticatorSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthenticatorSetupResponse>> SetupAuthenticator() =>
        this.ToActionResult(await twoFactorService.SetupAuthenticatorAsync(this.UserId()));

    [HttpPost("enable")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecoveryCodesResponse>> Enable(EnableTwoFactorRequest request, CancellationToken ct) =>
        this.ToActionResult(await twoFactorService.EnableAsync(this.UserId(), request, ct));

    [HttpPost("disable")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> Disable(PasswordRequest request, CancellationToken ct) =>
        this.ToActionResult(await twoFactorService.DisableAsync(this.UserId(), request, ct));

    [HttpPost("recovery-codes")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodes(PasswordRequest request) =>
        this.ToActionResult(await twoFactorService.RegenerateRecoveryCodesAsync(this.UserId(), request));
}
