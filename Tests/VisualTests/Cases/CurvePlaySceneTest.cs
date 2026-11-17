using System;
using System.IO;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Storage;
using Xanadu.Model.Settings;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Kinds;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The curve rework end to end, to watch: on a throwaway 120 BPM metronome, the
/// clean editor's curve tool lays a curve note click by click (the last two
/// points at 2×), then the level is played from the start and the head is hit
/// on its beat. Once hit, the path is an attractor line: the hovering blue
/// square keeps its own motion and is pulled toward the line, so it swings a
/// little wide on the bends; the test checks it never drifts far from the line.
/// </summary>
public sealed class CurvePlaySceneTest : SceneTest
{
    private const int Bpm = 120;
    private const double SongSeconds = 24;

    /// <summary>The curve starts on beat 3, so the square is seen waiting first.</summary>
    private const long StartMs = 1000;

    /// <summary>How long a curve may take to catch the square before it is held to it, ms.</summary>
    private const long CatchMs = 300;

    /// <summary>How far, in playfield units, the square may drift from the line once caught; the bends swing it wide.</summary>
    private const float MaxStray = 80f;

    /// <summary>Screen points the curve tool is clicked at: a wave across the view.</summary>
    private static readonly Vector2[] Clicks =
    {
        new(520, 560), new(680, 380), new(860, 600), new(1040, 400), new(1220, 580),
    };

    private LoadedSong? _song;
    private CurvePath? _path;
    private float _worstStray;
    private int _samples;

    public override string Name => "Curve play";
    public override string Slug => "curve-play";

    protected override void SetUpSteps()
    {
        AddStep("write a metronome song", WriteMetronome);

        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the editor",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorLeftMenu>().Count > 0);
        AddStep("give the level the metronome", () => Editor().GetComponent<LevelInfo>().Song = _song);
        AddUntilStep("the song picker is down", () =>
        {
            if (EditorDialogs.IsSongRequired(Context.Entities))
                return false;
            EditorDialogs.Dismiss(Context.Entities);
            return !EditorDialogs.AnyOpen(Context.Entities);
        });
        AddStep("start the old notes over", () => ItemKinds.Notes.DeleteAll(Context.Entities));
        AddAssert("the curve is the only tool", () =>
            PlaceButtons() == 1 &&
            Editor().GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Curve);

        ClickAndRelease("pick 1× speed", () => ScreenOf(StrengthButton(DashStrength.Single)));
        AddStep("put the playhead on beat 3", () => Editor().GetComponent<Playhead>().TimeMs = StartMs);
        for (int i = 0; i < Clicks.Length; i++)
        {
            Vector2 point = Clicks[i];
            if (i == 3)
                ClickAndRelease("pick 2× speed", () => ScreenOf(StrengthButton(DashStrength.Double)));
            ClickAndRelease($"click curve point {i + 1}", () => point);
        }

        AddAssert("no curve until it is finished", () => Context.Entities.Query<Note>().Count == 0);
        // Aimed at the point as laid, which grid snap may have moved off the click.
        ClickAndRelease("click the last point again to finish", () =>
            Editor().GetComponent<PlayfieldView>().PlayfieldToScreen(Editor().GetComponent<EditorPolygonDraft>().Points[^1]));
        AddUntilStep("one curve note was made", () => Context.Entities.Query<Note>().Count == 1);
        AddAssert("it is an action note with a path", () =>
            Curve().Kind == NoteKind.Hit && Curve().Target == KeybindAction.Action1 && Curve().IsCurve);
        AddAssert("its head is its first point", () => Curve().Position == Curve().Path!.Points[0]);
        AddAssert("it starts on beat 3", () => Curve().TimeMs == StartMs);
        AddAssert("it has a point per click", () => Curve().Path!.Count == Clicks.Length);
        AddAssert("its speeds are as picked", () =>
            Curve().Path!.Speeds.ToArray().SequenceEqual(new[] { 600f, 600f, 600f, 900f, 900f }));
        AddAssert("the editor counts it as a note", () => LevelDocumentMapper.Capture(Context.Entities, Editor()).Notes.Count == 1);
        AddWaitStep("look at the curve and the floor in the editor", 30);

        AddStep("play it from the start", () =>
        {
            _path = CurvePath.Of(Curve());
            LevelDocument document = LevelDocumentMapper.Capture(Context.Entities, Editor());
            Context.Scenes.StartEditorPreview(new LevelEntry("visual://curve-play", document, _song), 0);
        });
        AddUntilStep("the run has started", () =>
            Context.Scenes.Current == SceneKind.Play &&
            Context.Entities.Query<PlaySession>().Count == 1 &&
            Context.Entities.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Started);
        AddAssert("the classic character is unplugged", () =>
            Context.Entities.Query<CharacterRig>().Count == 0 && Context.Entities.Query<PhysicsBody>().Count == 0);

        AddUntilStep("the head is due", () => SongMs() >= StartMs - 8);
        AddStep("hit the head", () => Context.Keyboard.Press(Context.Systems.Keybinds.Get(KeybindAction.Action1)));
        AddWaitStep("hold", 2);
        AddStep("let go", () => Context.Keyboard.Release(Context.Systems.Keybinds.Get(KeybindAction.Action1)));
        AddAssert("the head was judged a hit and now steers", () =>
            Context.Entities.Query<Note>().AllTags(Tags.Get<NoteSteering>()).Count == 1);
        AddUntilStep("the square follows the curve to its end", Follow, timeoutSeconds: SongSeconds);
        AddStep("report how closely it followed", () =>
            Console.WriteLine($"[curve-play] {_samples} frames on the curve, worst stray {_worstStray:0.0} units"));
        AddAssert("the square was watched along the curve", () => _samples > 30, "too few frames on the curve to judge");
        AddAssert("it stayed near the line", () => _worstStray <= MaxStray,
            $"drifted further than {MaxStray} units from the line");
        AddWaitStep("watch it glide on", 90);
        AddStep("pause", () => Context.Scenes.PausePlay());
        AddWaitStep("hold pause", 2);
        AddStep("leave", () => Context.Scenes.LeavePause());
        AddUntilStep("back in the editor", () => Context.Scenes.Current == SceneKind.Editor);
    }

    /// <summary>A click on every beat, higher on the first of each bar of four, in the test's user folder.</summary>
    private void WriteMetronome()
    {
        string path = GamePaths.Full("user://curve-metronome.wav");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Metronome(Bpm, SongSeconds));
        }

        _song = TestContent.Song("curve-metronome", "Curve Metronome", path, Bpm);
    }

    /// <summary>16-bit mono PCM: a short decaying tone on each beat.</summary>
    private static byte[] Metronome(int bpm, double seconds)
    {
        const int rate = 44100;
        int samples = (int)(rate * seconds);
        int beat = rate * 60 / bpm;
        int click = rate * 40 / 1000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            int into = i % beat;
            bool bar = i / beat % 4 == 0;
            double value = 0;
            if (into < click)
            {
                double t = into / (double)rate;
                value = Math.Sin(Math.Tau * (bar ? 1600 : 1000) * t) * Math.Exp(-t * 90) * (bar ? 0.8 : 0.55);
            }

            writer.Write((short)(value * short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Samples how far the square is from the line each frame; done once the song is past the curve.</summary>
    private bool Follow()
    {
        if (_path is null)
            return false;

        foreach (Entity entity in Context.Entities.Query<CurveBody>().ToEntityList())
        {
            CurveBody body = entity.GetComponent<CurveBody>();
            if (body.OnCurve && body.SongMs >= _path.StartMs + CatchMs)
            {
                _worstStray = MathF.Max(_worstStray, Vector2.Distance(body.Position, body.Target));
                _samples++;
            }

            return body.SongMs > _path.EndMs;
        }

        return false;
    }

    private void ClickAndRelease(string description, Func<Vector2> at)
    {
        AddStep(description, () => Context.Pointer.Click(at()));
        AddWaitStep("hold", 2);
        AddStep("release", () => Context.Pointer.Release());
        AddWaitStep("settle", 1);
    }

    private Note Curve() =>
        Context.Entities.Query<Note>().ToEntityList()[0].GetComponent<Note>();

    private long SongMs()
    {
        foreach (Entity entity in Context.Entities.Query<CurveBody, Playhead>().ToEntityList())
            return entity.GetComponent<Playhead>().TimeMs;
        return 0;
    }

    private Entity Editor() =>
        Context.Entities.Query<EditorPlacement, PlayfieldView, NoteSelection>().ToEntityList()[0];

    private int PlaceButtons() =>
        Context.Entities.Query<UiCommand>().ToEntityList()
            .Count(entity => entity.GetComponent<UiCommand>().As<EditorButtonKind>() == EditorButtonKind.Place);

    private Entity StrengthButton(DashStrength strength)
    {
        foreach (Entity entity in Context.Entities.Query<UiCommand>().ToEntityList())
        {
            UiCommand button = entity.GetComponent<UiCommand>();
            if (button.As<EditorButtonKind>() == EditorButtonKind.DashStrength && button.Arg == (int)strength)
                return entity;
        }

        return default;
    }

    private static Vector2 ScreenOf(Entity entity) =>
        entity.IsNull ? new Vector2(-100, -100) : UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
}
