using IdentityService.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Application.Common;

internal static class IdentityResultExtensions
{
    public static Error ToError(this IdentityResult result) => new(
        ErrorType.Validation,
        Error.ValidationCode,
        "Wystąpiły błędy walidacji.",
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).Distinct().ToArray()));
}
