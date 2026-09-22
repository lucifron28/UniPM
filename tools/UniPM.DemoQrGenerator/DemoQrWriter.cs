using System.Net;
using System.Text;
using QRCoder;
using UniPM.Api.Data.Seeding;

namespace UniPM.DemoQrGenerator;

public static class DemoQrWriter
{
    public static async Task<DemoQrWriteResult> WriteAsync(
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        foreach (var asset in DevelopmentDemoCatalog.Assets)
        {
            var pngPath = Path.Combine(outputDirectory, $"{asset.AssetCode}.png");
            await File.WriteAllBytesAsync(
                pngPath,
                CreatePng(asset.QrCodeValue),
                cancellationToken);
        }

        var indexPath = Path.Combine(outputDirectory, "index.html");
        await File.WriteAllTextAsync(
            indexPath,
            BuildIndexHtml(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        return new DemoQrWriteResult(DevelopmentDemoCatalog.Assets.Count, indexPath);
    }

    public static byte[] CreatePng(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var qrCodeData = QRCodeGenerator.GenerateQrCode(
            payload,
            QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule: 20, drawQuietZones: true);
    }

    private static string BuildIndexHtml()
    {
        var cards = new StringBuilder();
        foreach (var asset in DevelopmentDemoCatalog.Assets)
        {
            cards.AppendLine("<article class=\"card\">");
            cards.AppendLine($"  <img src=\"{Encode(asset.AssetCode)}.png\" alt=\"QR code for {Encode(asset.AssetCode)}\">");
            cards.AppendLine($"  <h2>{Encode(asset.AssetCode)}</h2>");
            cards.AppendLine($"  <dl><dt>Department</dt><dd>{Encode(asset.Department)}</dd>");
            cards.AppendLine($"  <dt>Category</dt><dd>{Encode(ToDisplayCategory(asset.AssetCategory))}</dd>");
            cards.AppendLine($"  <dt>Location</dt><dd>{Encode(asset.Location)}</dd>");
            cards.AppendLine($"  <dt>QR payload</dt><dd class=\"payload\">{Encode(asset.QrCodeValue)}</dd></dl>");
            cards.AppendLine("</article>");
        }

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>UniPM fictional demo QR sheet</title>
              <style>
                :root { color-scheme: light; font-family: Arial, sans-serif; }
                body { margin: 0; padding: 28px; color: #101828; background: #eef2f6; }
                header { max-width: 1500px; margin: 0 auto 24px; }
                h1 { margin: 0 0 8px; font-size: 30px; }
                header p { margin: 0; color: #475467; }
                main { display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 24px; max-width: 1500px; margin: auto; }
                .card { padding: 24px; border: 2px solid #d0d5dd; border-radius: 16px; background: #fff; box-shadow: 0 8px 24px rgba(16, 24, 40, .08); break-inside: avoid; }
                img { display: block; width: min(100%, 420px); aspect-ratio: 1; margin: 0 auto 18px; image-rendering: pixelated; }
                h2 { margin: 0 0 14px; text-align: center; font-size: 28px; }
                dl { display: grid; grid-template-columns: 110px 1fr; gap: 8px 12px; margin: 0; font-size: 17px; }
                dt { font-weight: 700; }
                dd { margin: 0; overflow-wrap: anywhere; }
                .payload { font-family: Consolas, monospace; font-weight: 700; }
                @media print { body { padding: 0; background: #fff; } main { grid-template-columns: repeat(2, 1fr); } .card { box-shadow: none; page-break-inside: avoid; } }
              </style>
            </head>
            <body>
              <header>
                <h1>UniPM fictional demo QR codes</h1>
                <p>Development data only. Open this page at 100% zoom and scan a card from the mobile app.</p>
              </header>
              <main>
            {{cards}}
              </main>
            </body>
            </html>
            """;
    }

    private static string ToDisplayCategory(string category)
    {
        return category switch
        {
            "fire-extinguisher" => "Fire Extinguisher",
            "emergency-light" => "Emergency Light",
            _ => category
        };
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

public sealed record DemoQrWriteResult(int PngCount, string IndexPath);
