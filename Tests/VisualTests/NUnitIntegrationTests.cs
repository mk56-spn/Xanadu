using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Writer.Tests.VisualTests;

/// <summary>
/// One NUnit test per Godot scene, so Rider exposes a run arrow beside every
/// visual test. Each test launches the isolated visual-test project.
/// </summary>
[TestFixture]
[Explicit("Requires GODOT_BIN and a display-capable Godot build.")]
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

    private static void RunScene(string slug)
    {
        string godot = Environment.GetEnvironmentVariable("GODOT_BIN") ?? "";
        if (string.IsNullOrWhiteSpace(godot) || !File.Exists(godot))
        {
            Assert.Ignore(
                "Set GODOT_BIN to the Godot 4.8 mono executable to run scene tests.");
            return;
        }

        string root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../"));
        string project = Path.Combine(root, "Tests", "VisualTests");

        var start = new ProcessStartInfo
        {
            FileName = godot,
            WorkingDirectory = project,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("--path");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("res://VisualTests.tscn");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add($"--visual-test={slug}");
        start.ArgumentList.Add("--visual-test-exit");

        using Process? process = Process.Start(start);
        Assert.That(process, Is.Not.Null);
        string stdout = process!.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        Assert.That(
            process.WaitForExit(120_000),
            Is.True,
            $"{slug}: Godot did not exit.");
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
