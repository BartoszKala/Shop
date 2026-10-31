using IdentityService.Application.Abstractions;
using QRCoder;

namespace IdentityService.Infrastructure.QrCodes;

internal sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string GeneratePngDataUrl(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 5);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
