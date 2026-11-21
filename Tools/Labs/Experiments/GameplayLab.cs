using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Model.Levels;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A bare bench for gameplay work: a flat surface and the player as a plain
/// box, 2.5 tall to 1 wide (100 by 40 pixels), standing on it.
/// </summary>
internal sealed class GameplayLab : ILabExperiment
{
    /// <summary>The player box, in pixels: 2.5 tall to 1 wide.</summary>
    public static readonly Vector2 PlayerSize = new(40, 100);

    /// <summary>How far down the stage the surface's top sits (0 = top, 1 = bottom).</summary>
    private const float SurfaceAt = 0.75f;

    /// <summary>The background pulses on this tempo, a soft lift on each beat that fades out.</summary>
    private static readonly TempoMap PulseTempo = TempoMap.Single(80, 0);

    /// <summary>How far the background lifts toward <see cref="Palette.SurfaceAlt"/> on the beat.</summary>
    private const float PulseLift = 0.35f;

    /// <summary>How fast the lift fades after the beat (per second, exponential).</summary>
    private const float PulseDecay = 6f;

    public string Name => "GAMEPLAY";

    public string[] Notes =>
    [
        "A surface and the player as a box,",
        "2.5 tall by 1 wide (100 × 40 px).",
        "Background pulses at 80 BPM.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : IToolStage
    {
        private readonly Handle _background;
        private readonly Handle _item;
        private readonly Rect2 _area;

        public Stage(Handle parent, Rect2 area)
        {
            _area = area;
            _background = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_background, parent);
            CanvasServer.Current.AddRect(_background, area, Palette.Stage);

            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);

            float top = area.Position.Y + area.Size.Y * SurfaceAt;
            Vector2 surface = new(area.Position.X, top);
            CanvasServer.Current.AddRect(_item, new Rect2(surface, new Vector2(area.Size.X, area.End.Y - top)), Palette.SurfaceAlt);
            CanvasServer.Current.AddLine(_item, surface, new Vector2(area.End.X, top), Palette.BorderSubtle, 2, false);

            Vector2 feet = new(area.Position.X + area.Size.X * 0.5f, top);
            Vector2 corner = feet - new Vector2(PlayerSize.X * 0.5f, PlayerSize.Y);
            CanvasServer.Current.AddRect(_item, new Rect2(corner, PlayerSize), Palette.PrimarySoft);
        }

        public void Update(in ToolFrame frame)
        {
            double timeMs = frame.Time * 1000d;
            double beat = PulseTempo.BeatAt(timeMs);
            float sinceBeat = (float)((timeMs - PulseTempo.TimeAt(System.Math.Floor(beat))) / 1000d);
            float lift = PulseLift * System.MathF.Exp(-PulseDecay * sinceBeat);
            CanvasServer.Current.Clear(_background);
            CanvasServer.Current.AddRect(_background, _area, Palette.Stage.Lerp(Palette.SurfaceAlt, lift));
        }

        public void Dispose()
        {
            CanvasServer.Current.Free(_item);
            CanvasServer.Current.Free(_background);
        }
    }
}
