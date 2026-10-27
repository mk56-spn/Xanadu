using System.IO;
using NUnit.Framework;
using Xanadu.Settings;

namespace Xanadu.Tests.Settings;

[TestFixture]
public sealed class DisplaySettingsTests
{
    [Test]
    public void VSyncCanBeDisabledAndReloaded()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"writer-display-{System.Guid.NewGuid():N}.json");

        try
        {
            var settings = new DisplaySettings(
                path,
                persist: true,
                apply: false);
            Assert.That(settings.VSyncEnabled, Is.True);

            settings.SetVSync(false);
            Assert.That(settings.VSyncEnabled, Is.False);

            var reloaded = new DisplaySettings(
                path,
                persist: true,
                apply: false);
            Assert.That(reloaded.VSyncEnabled, Is.False);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
