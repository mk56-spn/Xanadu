using System;
using NUnit.Framework;
using Writer.Misc;

namespace Writer.Tests;

[TestFixture]
public sealed class ScreenshotNamingTests
{
    [Test]
    public void UsesGameNameDateAndReadableTime()
    {
        string file = ScreenshotNaming.Create(
            "Writer",
            new DateTime(2026, 10, 22, 2, 13, 30),
            _ => false);

        Assert.That(file, Is.EqualTo("Writer 2026-10-22 02-13-30.png"));
    }

    [Test]
    public void AddsASuffixWhenTheNameAlreadyExists()
    {
        string file = ScreenshotNaming.Create(
            "Writer",
            new DateTime(2026, 10, 22, 2, 13, 30),
            candidate => candidate == "Writer 2026-10-22 02-13-30.png");

        Assert.That(file, Is.EqualTo("Writer 2026-10-22 02-13-30 (2).png"));
    }
}
