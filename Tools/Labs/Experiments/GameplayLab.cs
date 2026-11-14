using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Host.Tools;

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

    public string Name => "GAMEPLAY";

    public string[] Notes =>
    [
        "A surface and the player as a box,",
        "2.5 tall by 1 wide (100 × 40 px).",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : IToolStage
    {
        private readonly Handle _item;

        public Stage(Handle parent, Rect2 area)
        {
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);

            float top = area.Position.Y + area.Size.Y * SurfaceAt;
            Vector2 surface = new(area.Position.X, top);
            CanvasServer.Current.AddRect(_item, area, Palette.Stage);
            CanvasServer.Current.AddRect(_item, new Rect2(surface, new Vector2(area.Size.X, area.End.Y - top)), Palette.SurfaceAlt);
            CanvasServer.Current.AddLine(_item, surface, new Vector2(area.End.X, top), Palette.BorderSubtle, 2, false);

            Vector2 feet = new(area.Position.X + area.Size.X * 0.5f, top);
            Vector2 corner = feet - new Vector2(PlayerSize.X * 0.5f, PlayerSize.Y);
            CanvasServer.Current.AddRect(_item, new Rect2(corner, PlayerSize), Palette.PrimarySoft);
        }

        public void Update(in ToolFrame frame)
        {
        }

        public void Dispose() => CanvasServer.Current.Free(_item);
    }
}
