using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Debug;

/// <summary>F1 frame rate readout in the top right, coloured by how close it is to the target.</summary>
public sealed class FpsCounterSystem(
    IOverlayToggleSource toggle,
    EntityStore store,
    IFpsSource fps,
    IVisualRidFactory? visuals = null) : QuerySystem<Rectangle, Position>
{
    /// <summary>Over the other debug panels, so it stays readable with them open.</summary>
    private const int FontSize = UiType.Section;
    private const float GlowRadius = 6f;
    private const float GlowIntensity = 0.8f;

    /// <summary>Sized for the widest reading, so the box does not change as the number does.</summary>
    private static readonly Vector2 BoxSize =
        UiNode.Measure("9999 FPS", FontSize) + new Vector2(UiMetrics.Space.Md, UiMetrics.Space.Xs) * 2;

    private readonly OverlayLatch _latch = new(toggle);
    private Entity _layer;
    private Entity _label;

    protected override void OnUpdate()
    {
        _latch.Update();
        if (!_latch.Visible)
        {
            Close();
            return;
        }

        if (_layer.IsNull)
            Open();

        double current = fps.GetFps();
        Color color = DebugPalette.Fps(current);
        UiText.Set(_label, $"{current:0} FPS");

        ref TextStyle style = ref _label.GetComponent<TextStyle>();
        ref TextGlow glow = ref _label.GetComponent<TextGlow>();
        if (style.Color == color)
            return;

        style.Color = color;
        glow.Color = DebugPalette.Glow(color);
        _label.AddTag<Dirty>();
    }

    private void Open()
    {
        UiNode layer = UiNode.Layer(store, default, UiLayers.FpsCounter, visuals, UiStage.Full).Tag<DebugUi>();
        _layer = layer.Entity;

        Vector2 stage = UiStage.Full.Size;
        float margin = UiMetrics.Space.Lg;
        UiNode box = layer.Column()
            .Size(BoxSize)
            .At(new Vector2(stage.X - margin - BoxSize.X / 2, margin + BoxSize.Y / 2))
            .Fill(DebugPalette.Panel)
            .Border(DebugPalette.PanelBorder)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        _label = box.Text("", FontSize, DebugPalette.Text, TextAlign.Center)
            .Size(BoxSize)
            .Glow(DebugPalette.Glow(DebugPalette.Text), GlowRadius, GlowIntensity)
            .Entity;
    }

    private void Close()
    {
        if (_layer.IsNull)
            return;

        UiLayout.DeleteSubtree(_layer);
        _layer = default;
    }
}
