using System;
using System.IO;
using NUnit.Framework;
using Writer.Settings;

namespace Writer.Tests.Settings;

[TestFixture]
public sealed class HudLayoutSettingsTests
{
    [Test]
    public void PersistsPositionAndVisibility()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "writer-hud-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var settings = new HudLayoutSettings(path);
            settings.Set("judgement_counts", 0.72f, 0.15f, false);

            var loaded = new HudLayoutSettings(path);
            Assert.That(loaded.TryGet("judgement_counts", out var entry), Is.True);
            Assert.That(entry.X, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(entry.Y, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(entry.Visible, Is.False);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
