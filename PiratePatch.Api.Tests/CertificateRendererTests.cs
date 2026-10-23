using System.Text;
using Microsoft.Extensions.Options;
using PiratePatch.Api.Certificates;
using PiratePatch.Api.Configuration;

namespace PiratePatch.Api.Tests;

public class CertificateRendererTests
{
    private static readonly SigningKey Key = new(new byte[32]);

    private static CertificateRenderer Renderer() => new(Options.Create(new AssetOptions()));

    [Fact]
    public void RendersAPdfForTheLongestPossibleCode()
    {
        var code = new FlagCodec(Key).Issue(long.MaxValue, new string('W', 32));

        var pdf = Renderer().Render(new CertificateData(new string('W', 32), code, new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), new Uri("https://ahoy.example.org/")));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 100_000);
    }

    [Fact]
    public void RenderingTwiceWorks()
    {
        var renderer = Renderer();
        var data = new CertificateData("blackbeard", new FlagCodec(Key).Issue(1, "blackbeard"), DateTimeOffset.UnixEpoch, new Uri("https://ahoy.example.org/"));

        Assert.NotEmpty(renderer.Render(data));
        Assert.NotEmpty(renderer.Render(data));
    }
}
