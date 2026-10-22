using System;
using System.IO;
using NUnit.Framework;
using Writer.Settings;

namespace Writer.Tests.Settings;

[TestFixture]
public sealed class OffsetSettingsTests
{
    [Test]
    public void PersistsAndReloadsTheOffset()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "writer-offset-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var settings = new OffsetSettings(path);
            settings.Set(42);
            Assert.That(settings.OffsetMs, Is.EqualTo(42));

            var loaded = new OffsetSettings(path);
            Assert.That(loaded.OffsetMs, Is.EqualTo(42));

            loaded.Reset();
            Assert.That(loaded.OffsetMs, Is.Zero);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
