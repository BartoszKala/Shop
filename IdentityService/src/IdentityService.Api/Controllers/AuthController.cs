using IdentityService.Api.Extensions;
using IdentityService.Application.Contracts;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.RegisterAsync(request, ct));

    [HttpPost("confirm-email")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.ConfirmEmailAsync(request, ct));

    [HttpPost("resend-confirmation")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ResendConfirmation(EmailRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.ResendConfirmationAsync(request, ct));

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.LoginAsync(request, ct));

    [HttpPost("login/2fa")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> LoginWithTwoFactor(TwoFactorLoginRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.LoginWithTwoFactorAsync(request, ct));

    [HttpPost("login/recovery-code")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> LoginWithRecoveryCode(RecoveryCodeLoginRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.LoginWithRecoveryCodeAsync(request, ct));

    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.RefreshAsync(request, ct));

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(RefreshTokenRequest request, CancellationToken ct) =>
        this.ToNoContent(await authService.LogoutAsync(request, ct));

    [HttpPost("forgot-password")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(EmailRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.ForgotPasswordAsync(request, ct));

    [HttpPost("reset-password")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken ct) =>
        this.ToActionResult(await authService.ResetPasswordAsync(request, ct));
}
