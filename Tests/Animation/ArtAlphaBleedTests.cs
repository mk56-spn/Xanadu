using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class ArtAlphaBleedTests
{
    [Test]
    public void TransparentPixelsTakeTheNearestVisibleColourAndStayTransparent()
    {
        // 4×1: red, clear, clear, blue.
        byte[] pixels =
        {
            255, 0, 0, 255,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 255, 128,
        };

        ArtAlphaBleed.Apply(pixels, 4, 1);

        Assert.That(pixels[4..8], Is.EqualTo(new byte[] { 255, 0, 0, 0 }));
        Assert.That(pixels[8..12], Is.EqualTo(new byte[] { 0, 0, 255, 0 }));
        Assert.That(pixels[0..4], Is.EqualTo(new byte[] { 255, 0, 0, 255 }), "visible pixels are untouched");
        Assert.That(pixels[12..16], Is.EqualTo(new byte[] { 0, 0, 255, 128 }));
    }

    [Test]
    public void TheBleedReachesPixelsFarFromAnyEdge()
    {
        const int size = 32;
        var pixels = new byte[size * size * 4];
        pixels[0] = 10;
        pixels[1] = 200;
        pixels[2] = 30;
        pixels[3] = 255;

        ArtAlphaBleed.Apply(pixels, size, size);

        int far = (size * size - 1) * 4;
        Assert.That(pixels[far..(far + 4)], Is.EqualTo(new byte[] { 10, 200, 30, 0 }));
    }

    [Test]
    public void FullyTransparentArtIsLeftAlone()
    {
        var pixels = new byte[2 * 2 * 4];

        ArtAlphaBleed.Apply(pixels, 2, 2);

        Assert.That(pixels, Is.All.EqualTo(0));
    }
}
