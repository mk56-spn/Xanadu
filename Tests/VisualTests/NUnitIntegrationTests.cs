using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Writer.Tests.VisualTests;

/// <summary>
/// Opt-in NUnit orchestration for the Godot scene tests. The scene tests cannot
/// run inside the NUnit host because they require Godot's main loop, rendering,
/// input, and native engine state.
/// </summary>
[TestFixture]
[Explicit("Requires GODOT_BIN and a display-capable Godot build.")]
public sealed class NUnitIntegrationTests
{
    private static readonly string[] Slugs =
    {
        "profiles",
        "main",
        "song-select",
        "editor-waveform",
        "editor-metadata",
        "settings",
        "offset-wizard",
        "play",
        "movement-judgement",
        "results",
        "restart",
        "editor-dropdowns",
        "leave-editor",
    };

    [Test]
    public void VisualSceneTestsPassThroughGodotProcess()
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
        var failures = new List<string>();

        foreach (string slug in Slugs)
        {
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

            using var process = Process.Start(start);
            Assert.That(process, Is.Not.Null);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(120_000);
            if (process.ExitCode != 0 ||
                !stdout.Contains($"[scene-test:{slug}] PASS", StringComparison.Ordinal))
            {
                failures.Add(
                    $"{slug} exited {process.ExitCode}\n{stdout}\n{stderr}");
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n\n", failures));
    }
}
