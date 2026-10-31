using IdentityService.Api.Extensions;
using IdentityService.Application.Contracts;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/privacy")]
[Produces("application/json")]
public sealed class PrivacyController(PrivacyService privacyService) : ControllerBase
{
    [HttpGet("consents")]
    [ProducesResponseType<ConsentsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsentsResponse>> Consents(CancellationToken ct) =>
        this.ToActionResult(await privacyService.GetConsentsAsync(this.UserId(), ct));

    [HttpPut("consents/marketing")]
    [ProducesResponseType<ConsentsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsentsResponse>> UpdateMarketingConsent(UpdateMarketingConsentRequest request, CancellationToken ct) =>
        this.ToActionResult(await privacyService.UpdateMarketingConsentAsync(this.UserId(), request, ct));

    [HttpPost("accept-terms")]
    [ProducesResponseType<ConsentsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsentsResponse>> AcceptTerms(CancellationToken ct) =>
        this.ToActionResult(await privacyService.AcceptCurrentTermsAsync(this.UserId(), ct));

    [HttpGet("export")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AccountDataExport>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountDataExport>> Export(CancellationToken ct)
    {
        var result = await privacyService.ExportAsync(this.UserId(), ct);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error);
        }

        Response.Headers.ContentDisposition = $"attachment; filename=\"moje-dane-{result.Value.ExportedAt:yyyyMMdd}.json\"";
        return Ok(result.Value);
    }

    [HttpPost("delete-account")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> DeleteAccount(DeleteAccountRequest request, CancellationToken ct) =>
        this.ToActionResult(await privacyService.DeleteAccountAsync(this.UserId(), request, ct));
}
