using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Core.Timeline;
using Xanadu.Core.Tweens;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Play;

/// <summary>
/// Animates the combo counter with tweens: every hit swells the number and
/// lights it in the accent colour, every fiftieth hit harder; a break flashes
/// number and caption red. The swell is a scale after layout, so the text keeps
/// its size and place; the colours are layers over the labels' own. The
/// counter's text and colours are written only when they change.
/// </summary>
public sealed class ComboHudSystem : QuerySystem<ComboHud, ScoreState>
{
    /// <summary>The number's font size.</summary>
    private const int Size = UiType.Display;

    /// <summary>How much bigger a hit makes the number at its peak.</summary>
    private const float Swell = 0.4f;

    /// <summary>Every this many hits the counter swells harder.</summary>
    private const int Milestone = 50;

    private const float MilestonePunch = 1.8f;

    // A reversed expo-in clip falls as 2^(-10 t / length): these lengths make
    // it e^(-10 t) for the swell and e^(-3 t) for the red, the old decays.
    private const float PunchSeconds = 0.6931472f;
    private const float BreakSeconds = 2.3104906f;

    private const ushort Punch = UiTweenKeys.FirstFree + 10;
    private const ushort Lit = UiTweenKeys.FirstFree + 11;
    private const ushort Broken = UiTweenKeys.FirstFree + 12;

    protected override void OnUpdate() => Query.Each(new ShowCombo(CommandBuffer));

    /// <summary>Starts a swell or a red flash when the combo moved; the tweens ease them off.</summary>
    private static void Play(ref ComboHud hud, int combo, TweenTarget number, TweenTarget caption)
    {
        TweenPool pool = UiTween.Pool;
        if (combo > hud.Shown)
        {
            float peak = combo % Milestone == 0 ? MilestonePunch : 1f;
            Clip decay = Clip.FromNow(PunchSeconds).Eased(EasePreset.ExpoIn).Reversed();
            pool.Play(number, Tween.ScaleTo(Vector2.One * (1f + Swell * peak), decay).Named(Punch));
            pool.Play(number, Tween.FadeTo(Palette.Accent, decay).Named(Lit));
            pool.Stop(number, Broken);
            pool.Stop(caption, Broken);
        }
        else if (combo < hud.Shown && hud.Shown > 1)
        {
            Clip decay = Clip.FromNow(BreakSeconds).Eased(EasePreset.ExpoIn).Reversed();
            pool.Stop(number, Punch);
            pool.Stop(number, Lit);
            pool.Play(number, Tween.FadeTo(Palette.Danger, decay, priority: 1).Named(Broken));
            pool.Play(caption, Tween.FadeTo(Palette.Danger, decay, priority: 1).Named(Broken));
        }

        hud.Shown = combo;
    }

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

    private readonly record struct ShowCombo(CommandBuffer Buffer) : IEach<ComboHud, ScoreState>
    {
        public void Execute(ref ComboHud hud, ref ScoreState score)
        {
            if (hud.Number.IsNull || hud.Caption.IsNull)
                return;

            TweenTarget number = UiTween.Target(hud.Number, Buffer);
            TweenTarget caption = UiTween.Target(hud.Caption, Buffer);
            Play(ref hud, score.Combo, number, caption);
            UiText.Set(hud.Number, score.Combo.ToString(), Buffer);
            Color rest = score.Combo > 0 ? Palette.Text : Palette.TextDim;
            Style(hud.Number, Size, UiTween.Pool.Colour(number, rest), Buffer);
            Style(hud.Caption, UiType.Small, UiTween.Pool.Colour(caption, Palette.TextDim), Buffer);
        }
    }
}
