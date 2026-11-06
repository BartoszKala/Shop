using System.Globalization;
using System.Text;

namespace ShopService.Domain.Common;

public static class Slug
{
    public const int MaxLength = 100;

    public static string From(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasDash = true;
        foreach (var c in text.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().TrimEnd('-');
        return slug.Length > MaxLength ? slug[..MaxLength].TrimEnd('-') : slug;
    }

    public static bool IsValid(string slug) =>
        slug.Length is > 0 and <= MaxLength && slug == From(slug);
}
