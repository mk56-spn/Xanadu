using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// One NUnit test per scene test, so Rider exposes a run arrow beside every
/// visual test. Each test launches the visual-test exe on the MoonWorks host.
/// </summary>
[TestFixture]
[Category("Visual")]
public sealed class VisualSceneTests
{
    [Test]
    public void Profiles() => RunScene("profiles");

    [Test]
    public void MainMenu() => RunScene("main");

    [Test]
    public void SongSelect() => RunScene("song-select");

    [Test]
    public void EditorWaveform() => RunScene("editor-waveform");

    [Test]
    public void EditorMetadata() => RunScene("editor-metadata");

    [Test]
    public void Settings() => RunScene("settings");

    [Test]
    public void OffsetWizard() => RunScene("offset-wizard");

    [Test]
    public void Play() => RunScene("play");

    [Test]
    public void MovementJudgement() => RunScene("movement-judgement");

    [Test]
    public void Results() => RunScene("results");

    [Test]
    public void Restart() => RunScene("restart");

    [Test]
    public void EditorDropdowns() => RunScene("editor-dropdowns");

    [Test]
    public void LeaveEditor() => RunScene("leave-editor");

    [Test]
    public void EditorExitMenu() => RunScene("editor-exit-menu");

    /// <summary>
    /// Runs one scene in the visual-test exe. Each opens a window, so they only
    /// run when <c>XANADU_VISUAL_TESTS=1</c> is set.
    /// </summary>
    private static void RunScene(string slug)
    {
        if (Environment.GetEnvironmentVariable("XANADU_VISUAL_TESTS") != "1")
        {
            Assert.Ignore("Set XANADU_VISUAL_TESTS=1 to run scene tests (each opens a window).");
            return;
        }

        string harness = Path.Combine(TestContext.CurrentContext.TestDirectory, "Xanadu.VisualTests.dll");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = TestContext.CurrentContext.TestDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(harness);
        start.ArgumentList.Add($"--visual-test={slug}");
        start.ArgumentList.Add("--visual-test-exit");

        using Process? process = Process.Start(start);
        Assert.That(process, Is.Not.Null);
        string stdout = process!.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        Assert.That(
            process.WaitForExit(120_000),
            Is.True,
            $"{slug}: the harness did not exit.");
        Assert.That(
            process.ExitCode,
            Is.Zero,
            $"{slug} failed.\n{stdout}\n{stderr}");
        Assert.That(
            stdout,
            Does.Contain($"[scene-test:{slug}] PASS"),
            $"{slug} did not report a pass.\n{stdout}\n{stderr}");
    }
}
