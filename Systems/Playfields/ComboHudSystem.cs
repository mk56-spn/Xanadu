using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Animates the combo counter: every hit swells the number and flashes it in
/// the accent colour, every fiftieth hit harder; a break flashes it red. The
/// counter's text, size and colour are written only when they change.
/// </summary>
public sealed class ComboHudSystem : QuerySystem<ComboHud, ScoreState>
{
    /// <summary>The number's font size at rest.</summary>
    public const int Size = UiType.Display;

    /// <summary>How much bigger a hit makes the number at its peak.</summary>
    public const float Swell = 0.4f;

    /// <summary>Every this many hits the counter swells harder.</summary>
    public const int Milestone = 50;

    private const float MilestonePunch = 1.8f;
    private const float PunchDecay = 10f;
    private const float BreakDecay = 3f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        float delta = Tick.deltaTime;
        Query.ForEachEntity((ref hud, ref score, _) =>
        {
            Step(ref hud, score.Combo, delta);
            UiText.Set(hud.Number, score.Combo.ToString(), buffer);
            Style(hud.Number, FontSize(hud), NumberColor(hud, score.Combo), buffer);
            Style(hud.Caption, UiType.Small, CaptionColor(hud), buffer);
        });
    }

    /// <summary>Starts a swell or a red flash when the combo moved, then lets both ease off.</summary>
    public static void Step(ref ComboHud hud, int combo, float delta)
    {
        if (combo > hud.Shown)
        {
            hud.Punch = combo % Milestone == 0 ? MilestonePunch : 1f;
            hud.Break = 0f;
        }
        else if (combo < hud.Shown && hud.Shown > 1)
        {
            hud.Break = 1f;
            hud.Punch = 0f;
        }

        hud.Shown = combo;
        hud.Punch *= MathF.Exp(-PunchDecay * delta);
        hud.Break *= MathF.Exp(-BreakDecay * delta);
        if (hud.Punch < 0.01f)
            hud.Punch = 0f;
        if (hud.Break < 0.01f)
            hud.Break = 0f;
    }

    public static int FontSize(ComboHud hud) =>
        (int)MathF.Round(Size * (1f + Swell * hud.Punch));

    private static Color NumberColor(ComboHud hud, int combo)
    {
        Color rest = combo > 0 ? Palette.Text : Palette.TextDim;
        Color color = rest.Lerp(Palette.Accent, Math.Clamp(hud.Punch, 0f, 1f));
        return color.Lerp(Palette.Danger, hud.Break);
    }

    private static Color CaptionColor(ComboHud hud) =>
        Palette.TextDim.Lerp(Palette.Danger, hud.Break);

    private static void Style(Entity entity, int size, Color color, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<TextStyle>())
            return;

        ref TextStyle style = ref entity.GetComponent<TextStyle>();
        if (style.FontSize == size && style.Color == color)
            return;

        style.FontSize = size;
        style.Color = color;
        buffer.AddTag<Dirty>(entity.Id);
    }
}
