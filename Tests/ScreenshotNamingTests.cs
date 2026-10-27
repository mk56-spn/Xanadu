using System;
using NUnit.Framework;
using Xanadu.Misc;

namespace Xanadu.Tests;

[TestFixture]
public sealed class ScreenshotNamingTests
{
    [Test]
    public void UsesGameNameDateAndReadableTime()
    {
        string file = ScreenshotNaming.Create(
            "Xanadu",
            new DateTime(2026, 10, 22, 2, 13, 30),
            _ => false);

        Assert.That(file, Is.EqualTo("Xanadu 2026-10-22 02-13-30.png"));
    }

    [Test]
    public void AddsASuffixWhenTheNameAlreadyExists()
    {
        string file = ScreenshotNaming.Create(
            "Xanadu",
            new DateTime(2026, 10, 22, 2, 13, 30),
            candidate => candidate == "Xanadu 2026-10-22 02-13-30.png");

        Assert.That(file, Is.EqualTo("Xanadu 2026-10-22 02-13-30 (2).png"));
    }
}
