using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Layering;

namespace Xanadu.Tests.Animation;

/// <summary>Step 12 of Docs/render-architecture.md: the animator sorts through the shared draw order.</summary>
[TestFixture]
public sealed class AnimatorDrawOrderTests
{
    [Test]
    public void AttachmentsKeepTheirListOrderWithinALayer()
    {
        for (int i = 1; i < 8; i++)
        {
            Assert.That(
                DrawOrder.Banded(-3, AnimatorDrawOrder.Attachment(i)),
                Is.GreaterThan(DrawOrder.Banded(-3, AnimatorDrawOrder.Attachment(i - 1))));
        }
    }

    [Test]
    public void ClothChainsAndEffectsFollowAttachmentsOnTheSameLayer()
    {
        int attachment = DrawOrder.Banded(2, AnimatorDrawOrder.Attachment(40));
        int cloth = DrawOrder.Banded(2, AnimatorDrawOrder.Cloth(0));
        int chain = DrawOrder.Banded(2, AnimatorDrawOrder.Chain(0, 0));
        int effect = DrawOrder.Banded(2, AnimatorDrawOrder.Effect(0, 0));

        Assert.That(cloth, Is.GreaterThan(attachment));
        Assert.That(chain, Is.GreaterThan(cloth));
        Assert.That(effect, Is.GreaterThan(chain));
    }

    [Test]
    public void ALaterLayerAlwaysDrawsOverAnEarlierOnesLastItem()
    {
        int low = DrawOrder.Banded(1, AnimatorDrawOrder.Effect(999, 99));
        int high = DrawOrder.Banded(2, AnimatorDrawOrder.Attachment(0));

        Assert.That(high, Is.GreaterThan(low));
    }

    [Test]
    public void OrdersStayInsideTheirBand()
    {
        Assert.That(AnimatorDrawOrder.Attachment(int.MaxValue), Is.EqualTo(AnimatorDrawOrder.ClothBand - 1));
        Assert.That(AnimatorDrawOrder.Cloth(int.MaxValue), Is.LessThan(AnimatorDrawOrder.ChainBand));
        Assert.That(AnimatorDrawOrder.Chain(int.MaxValue, int.MaxValue), Is.LessThan(AnimatorDrawOrder.EffectBand));
        Assert.That(AnimatorDrawOrder.Effect(int.MaxValue, int.MaxValue), Is.LessThan(100_000));
    }

    [Test]
    public void TheLayerPanelsRowOrderIsTheCanvasOrder()
    {
        for (int index = 0; index < 6; index++)
        {
            DrawSlot attachment = AnimatorDrawOrder.Slot(-1, cloth: false, index);
            DrawSlot cloth = AnimatorDrawOrder.Slot(-1, cloth: true, index);
            Assert.That(attachment.CompareTo(cloth), Is.LessThan(0), "cloth draws over attachments on one layer");
            Assert.That(
                DrawOrder.Banded(-1, AnimatorDrawOrder.Attachment(index)),
                Is.LessThan(DrawOrder.Banded(-1, AnimatorDrawOrder.Cloth(index))));
        }
    }
}
