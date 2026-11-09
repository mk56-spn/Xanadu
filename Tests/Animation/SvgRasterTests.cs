using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class SvgRasterTests
{
    private const string Square =
        """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="5"><rect x="0" y="0" width="5" height="5" fill="#ff0000"/></svg>""";

    [Test]
    public void TheImageIsTheDocumentsSizeTimesTheScale()
    {
        RasterImage image = SvgRaster.Render(Square, 4f);
        Assert.That((image.Width, image.Height), Is.EqualTo((40, 20)));
        Assert.That(image.Pixels, Has.Length.EqualTo(40 * 20 * 4));
    }

    [Test]
    public void PaintIsOpaqueAndStraightAndTheRestIsClear()
    {
        RasterImage image = SvgRaster.Render(Square, 4f);
        Assert.That(image.AlphaAt(0.2f, 0.5f), Is.EqualTo(1f));
        Assert.That(image.AlphaAt(0.8f, 0.5f), Is.Zero);

        int inside = (10 * image.Width + 5) * 4;
        Assert.That(image.Pixels[inside..(inside + 4)], Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
    }
}
