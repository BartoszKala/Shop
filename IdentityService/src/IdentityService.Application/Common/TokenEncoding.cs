using System.Buffers.Text;
using System.Text;

namespace IdentityService.Application.Common;

public static class TokenEncoding
{
    public static string Encode(string token) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));

    public static bool TryDecode(string code, out string token)
    {
        try
        {
            token = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(code));
            return true;
        }
        catch (FormatException)
        {
            token = string.Empty;
            return false;
        }
    }
}
