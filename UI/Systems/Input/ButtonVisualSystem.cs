using Xanadu.Core;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Core.Tweens;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Input;

/// <summary>
/// Button hover fades and click flashes, as tween layers over the button's own
/// colour: hover fades in and out (turning round mid-way without a jump), the
/// flash sits above it and fades out. The fill only changes while one runs.
/// </summary>
public sealed class ButtonVisualSystem : QuerySystem<ButtonVisual, FillColor>
{
    /// <summary>A whole hover fade, out to in; a part of the way takes its share.</summary>
    private const float HoverSeconds = 1f / 12f;

    private const float FlashSeconds = 0.12f;
    private const short HoverPriority = 0;
    private const short FlashPriority = 1;

    public ButtonVisualSystem() => Filter.WithoutAnyTags(Tags.Get<UiDisabled>());

    protected override void OnUpdate()
    {
        Query.EachEntity(new AnimateButtons(Query.Store, CommandBuffer));
    }

    private readonly record struct AnimateButtons(EntityStore Store, CommandBuffer Buffer) : IEachEntity<ButtonVisual, FillColor>
    {
        public void Execute(ref ButtonVisual visual, ref FillColor fill, int id)
        {
            Entity entity = Store.GetEntityById(id);
            bool hovered = entity.Tags.Has<Hovered>();
            bool pressed = entity.Tags.Has<Pressed>();
            TweenTarget target = UiTween.Of(entity);
            if (target.IsNone && !hovered && !pressed)
            {
                // Nothing has tweened it yet: it shows its own colour, which its owner may change.
                Show(ref fill, visual.Normal, id);
                return;
            }

            if (target.IsNone)
                target = UiTween.Target(entity, Buffer);

            TweenPool pool = UiTween.Pool;
            // Colours may change while a layer shows (a toggle lighting up): the layers follow.
            pool.SetValue(target, UiTweenKeys.Hover, Rgba(visual.Hover));
            pool.SetValue(target, UiTweenKeys.Flash, Rgba(visual.Flash));
            pool.Fade(target, Tween.FadeTo(visual.Hover, Clip.FromNow(HoverSeconds), HoverPriority).Named(UiTweenKeys.Hover), hovered ? 1f : 0f);
            if (pressed && !visual.WasPressed)
            {
                pool.Play(target, Tween.FadeTo(visual.Flash, Clip.FromNow(FlashSeconds).Reversed(), FlashPriority)
                    .Named(UiTweenKeys.Flash));
            }

            visual.WasPressed = pressed;
            Show(ref fill, pool.Colour(target, visual.Normal), id);
        }

        private void Show(ref FillColor fill, Color color, int id)
        {
            if (fill.Value == color)
                return;

            fill.Value = color;
            Buffer.AddTag<Dirty>(id);
        }
    }

    private static Vector4 Rgba(Color c) => new(c.R, c.G, c.B, c.A);
}
