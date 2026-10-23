using System.Globalization;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using PiratePatch.Api.Configuration;

namespace PiratePatch.Api.Certificates;

public sealed record CertificateData(string Handle, string Code, DateTimeOffset AwardedAt, Uri VerifyUrl);

/// <summary>Stamps a player's handle and treasure code onto the commendation template.</summary>
public sealed class CertificateRenderer
{
    // Layout of Assets/template.pdf, in points from the top-left of its single 2560 x 1768 page.
    private const string FontFamily = "IM Fell English";
    private const double HandleX = 522, HandleY = 472, HandleSize = 59;
    private const double CodeX = 110, CodeY = 1060, CodeSize = 44, CodeLineHeight = 54, CodeMaxWidth = 2330, CodeMaxLines = 3;
    private const double MetaX = 110, MetaY = 1690, MetaSize = 38;

    private static readonly XSolidBrush Ink = new(XColor.FromArgb(145, 23, 23));

    private readonly byte[] _template;

    public CertificateRenderer(IOptions<AssetOptions> assets)
    {
        _template = File.ReadAllBytes(assets.Value.CertificateTemplate);
        FontResolver.Register(File.ReadAllBytes(assets.Value.CertificateFont));
    }

    public byte[] Render(CertificateData data)
    {
        using var input = new MemoryStream(_template);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        using var gfx = XGraphics.FromPdfPage(document.Pages[0]);

        var handleFont = new XFont(FontFamily, HandleSize);
        var codeFont = new XFont(FontFamily, CodeSize);
        var metaFont = new XFont(FontFamily, MetaSize);

        gfx.DrawString($"@{data.Handle}", handleFont, Ink, new XPoint(HandleX, HandleY), XStringFormats.TopLeft);

        var lines = Wrap(gfx, data.Code, codeFont, CodeMaxWidth);
        if (lines.Count > CodeMaxLines)
            throw new InvalidOperationException("The treasure code does not fit on the certificate.");
        for (var i = 0; i < lines.Count; i++)
            gfx.DrawString(lines[i], codeFont, Ink, new XPoint(CodeX, CodeY + i * CodeLineHeight), XStringFormats.TopLeft);

        var meta = string.Create(CultureInfo.InvariantCulture, $"Awarded {data.AwardedAt:yyyy-MM-dd}  -  Check the seal at {data.VerifyUrl.Host}{data.VerifyUrl.AbsolutePath.TrimEnd('/')}/verify");
        gfx.DrawString(meta, metaFont, Ink, new XPoint(MetaX, MetaY), XStringFormats.TopLeft);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    /// <summary>Breaks a string without spaces into lines that fit <paramref name="maxWidth"/>.</summary>
    private static List<string> Wrap(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var length = 1;
            while (start + length < text.Length && gfx.MeasureString(text.Substring(start, length + 1), font).Width <= maxWidth)
                length++;

            lines.Add(text.Substring(start, length));
            start += length;
        }

        return lines;
    }

    private sealed class FontResolver(byte[] fontData) : IFontResolver
    {
        private static readonly Lock Gate = new();
        private static bool _registered;

        /// <summary>PDFsharp's resolver is process-wide and write-once.</summary>
        public static void Register(byte[] fontData)
        {
            lock (Gate)
            {
                if (_registered)
                    return;
                GlobalFontSettings.FontResolver = new FontResolver(fontData);
                _registered = true;
            }
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            string.Equals(familyName, FontFamily, StringComparison.OrdinalIgnoreCase) ? new FontResolverInfo(FontFamily) : null;

        public byte[]? GetFont(string faceName) => faceName == FontFamily ? fontData : null;
    }
}
