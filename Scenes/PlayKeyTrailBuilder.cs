using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The key trail as a container tree: a framed row of lanes, each a history
/// column over its keycap. The frame is the HUD item's only member and its
/// drag box is the frame's own rect, so dragging moves exactly what is drawn.
/// <see cref="PlayKeyTrailHudSystem"/> fills the history columns.
/// </summary>
internal static class PlayKeyTrailBuilder
{
    private const float KeySize = UiMetrics.Control.Lg;
    private const float LaneGap = UiMetrics.Space.Sm;
    private const float KeyGap = UiMetrics.Space.Xs;
    private const float FramePad = UiMetrics.Space.Md;
    private const float HistoryHeight = 180;

    /// <summary>Releases faster than this many per second per lane drop their oldest trails.</summary>
    private const int SlotsPerLane = 16;

    /// <summary>The frame's size; the auto-sized containers come out the same.</summary>
    public static Vector2 Size
    {
        get
        {
            int lanes = KeybindSettings.Actions.Length;
            return new Vector2(
                lanes * KeySize + (lanes - 1) * LaneGap + FramePad * 2,
                HistoryHeight + KeyGap + KeySize + FramePad * 2);
        }
    }

    /// <summary>Builds the trail centred on <paramref name="center"/> and returns its frame.</summary>
    public static Entity Build(UiNode ui, Entity playfield, Vector2 center)
    {
        string[]? labels = playfield.TryGetComponent(out KeyTrailLabels keyLabels)
            ? keyLabels.Labels
            : null;
        int laneCount = KeybindSettings.Actions.Length;
        var historySize = new Vector2(KeySize, HistoryHeight);

        UiNode frame = ui.Column()
            .Gap(0)
            .Pad(FramePad)
            .Fill(new Color(0f, 0f, 0f, 0.3f))
            .Border(new Color(Palette.Gold, 0.8f), UiMetrics.Border.Thin)
            .At(center);
        UiNode lanes = frame.Row().Gap(LaneGap);

        var keys = new Entity[laneCount];
        var outlines = new Entity[laneCount * SlotsPerLane];
        var cores = new Entity[laneCount * SlotsPerLane];

        for (int lane = 0; lane < laneCount; lane++)
        {
            KeybindAction action = KeybindSettings.Actions[lane];
            Color color = PlayfieldRenderer.ColorFor(action);
            UiNode column = lanes.Column().Gap(KeyGap);

            UiNode history = column.Column().Size(historySize);
            for (int slot = 0; slot < SlotsPerLane; slot++)
            {
                int index = lane * SlotsPerLane + slot;
                outlines[index] = history.Rect(color, size: Vector2.One)
                    .Border(Colors.White, UiMetrics.Border.Thin)
                    .IgnoreLayout()
                    .Hidden()
                    .Entity;
                cores[index] = history.Rect(color, size: Vector2.One)
                    .IgnoreLayout()
                    .Hidden()
                    .Entity;
            }

            string label = labels is not null && lane < labels.Length
                ? labels[lane]
                : KeybindSettings.LabelFor(action);
            keys[lane] = column.Rect(PlayKeyTrailHudSystem.IdleKeyFill, label, new Vector2(KeySize, KeySize))
                .Border(color, UiMetrics.Border.Thin)
                .Font(UiType.Caption)
                .TextColor(color)
                .Entity;
        }

        playfield.AddComponent(new KeyTrailHud(keys, outlines, cores, SlotsPerLane, historySize));
        return frame.Entity;
    }

    /// <summary>
    /// Where the trail sits by default: beside the field's left edge, level with
    /// its bottom, or against the screen's left inset when the field fills the width.
    /// </summary>
    public static Vector2 DefaultCenter(Vector2 origin, float shownBottom)
    {
        Vector2 size = Size;
        float x = Math.Max(
            origin.X - UiMetrics.Space.Md - size.X / 2,
            PlayScene.SideInset + size.X / 2);
        return new Vector2(x, shownBottom - size.Y / 2);
    }
}
