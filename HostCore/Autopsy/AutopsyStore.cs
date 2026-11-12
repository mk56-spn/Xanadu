using System;
using System.Collections.Generic;
using System.IO;
using Xanadu.Storage;

namespace Xanadu.Host.Autopsy;

/// <summary>A saved capture: which exe, when its launch ended. Open it through <see cref="AutopsyStore.Load"/>.</summary>
public sealed class AutopsyCapture
{
    internal AutopsyCapture(string app, DateTime saved, string file, string? trace)
    {
        App = app;
        Saved = saved;
        File = file;
        Trace = trace;
    }

    public string App { get; }
    public DateTime Saved { get; }
    internal string File { get; }

    /// <summary>The launch's CPU sample trace, when it ran with XANADU_PROFILE=1.</summary>
    internal string? Trace { get; }

    public bool HasTrace => Trace is not null;
}

/// <summary>
/// The one owner of frame autopsy files: <c>user://autopsy/&lt;app&gt;.xfa</c>,
/// one per exe, replaced by each launch when it closes, and beside it the
/// launch's CPU sample trace (<c>.nettrace</c>, plus the <c>.etlx</c> TraceEvent
/// converts it to) when it ran with XANADU_PROFILE=1.
/// </summary>
public static class AutopsyStore
{
    private static string Folder => GamePaths.Full("user://autopsy");

    public static void Save(FrameAutopsy autopsy)
    {
        string file = Path.Combine(Folder, autopsy.App + XfaBinary.Extension);
        string temporary = file + ".tmp";
        BinaryFile.Save(temporary, XfaBinary.Write(autopsy));
        File.Move(temporary, file, overwrite: true);
        // A trace from an earlier launch would be read as this one's.
        if (!autopsy.HasTrace)
            DeleteTrace(autopsy.App);
    }

    private static string TraceFile(string app) => Path.Combine(Folder, app + ".nettrace");

    /// <summary>Where a sampler streams its trace while the launch runs; <see cref="FinishTrace"/> puts it in place.</summary>
    internal static Stream CreateTrace(string app)
    {
        Directory.CreateDirectory(Folder);
        return new FileStream(TraceFile(app) + ".part", FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    internal static void FinishTrace(string app)
    {
        DeleteTrace(app);
        File.Move(TraceFile(app) + ".part", TraceFile(app), overwrite: true);
    }

    private static void DeleteTrace(string app)
    {
        File.Delete(TraceFile(app));
        File.Delete(Path.ChangeExtension(TraceFile(app), ".etlx"));
    }

    /// <summary>Every saved capture, most recent first.</summary>
    public static List<AutopsyCapture> List()
    {
        var captures = new List<AutopsyCapture>();
        if (!Directory.Exists(Folder))
            return captures;
        foreach (string file in Directory.GetFiles(Folder, "*" + XfaBinary.Extension))
        {
            string app = Path.GetFileNameWithoutExtension(file);
            string trace = TraceFile(app);
            captures.Add(new AutopsyCapture(app, File.GetLastWriteTime(file), file, File.Exists(trace) ? trace : null));
        }
        captures.Sort((a, b) => b.Saved.CompareTo(a.Saved));
        return captures;
    }

    /// <summary>The CPU sample trace for a reader (TraceEvent wants a path); null when the launch had none.</summary>
    internal static string? TracePath(AutopsyCapture capture) => capture.Trace is { } trace && File.Exists(trace) ? trace : null;

    public static FrameAutopsy Load(AutopsyCapture capture) => XfaBinary.Read(File.ReadAllBytes(capture.File));
}
