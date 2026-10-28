using NUnit.Framework;
using Xanadu.Systems.Audio;

namespace Xanadu.Tests.Audio;

[TestFixture]
public sealed class SpectrumGroupsTests
{
    [Test]
    public void EachGroupAveragesItsShareOfTheBandsLowestFirst()
    {
        float[] bands = [1, 1, 0, 0, 0.5f, 0.5f, 0, 0];
        var groups = new float[4];

        SpectrumGroups.Average(bands, groups);

        Assert.That(groups, Is.EqualTo(new[] { 1f, 0f, 0.5f, 0f }));
    }

    [Test]
    public void MoreGroupsThanBandsStillFillsEveryGroup()
    {
        var groups = new float[4];

        SpectrumGroups.Average([0.8f, 0.2f], groups);

        Assert.That(groups[0], Is.EqualTo(0.8f));
        Assert.That(groups[3], Is.EqualTo(0.2f));
    }

    [Test]
    public void NoBandsGivesSilence()
    {
        var groups = new float[] { 9, 9, 9, 9 };

        SpectrumGroups.Average([], groups);

        Assert.That(groups, Is.All.EqualTo(0f));
    }
}
