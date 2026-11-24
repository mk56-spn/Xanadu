using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>
/// The UI's place on the canvas. Its drawn entities live in a tree the host
/// keeps on the GPU (Docs/host.md §5.3): <see cref="UiResidentSync"/>
/// sends what changed, and the host places, orders and draws the rest, so a
/// quiet frame walks nothing. Nothing is mirrored into a parented item tree:
/// moving, hiding, fading or reordering an entity is just changing it.
/// </summary>
/// <remarks>
/// An entity is drawn when it has <see cref="Rectangle"/>, <see cref="Drawable"/>,
/// <see cref="VisualItem"/> and <see cref="Position"/>. It sits in its
/// parent's space when its parent is drawn, else in its layer's: the layer above
/// it in the tree, or for an entity outside any layer, the one its
/// <see cref="Drawable.ZIndex"/> names. Disabled entities and those tagged
/// <see cref="CanvasHidden"/> hide with everything under them.
/// </remarks>
public sealed class UiCanvasMount(EntityStore store) : ICanvasMount
{
    private readonly UiResidentSync _sync = new(store);

    /// <summary>A host that keeps no tree (headless) draws nothing, so there is nothing to list.</summary>
    public void Collect(ICanvasCollector into)
    {
        if (into.Resident(this) is not { } tree)
            return;

        _sync.Sync(tree, into);
        into.Draw(tree);
    }

    /// <summary>Where an entity draws among its siblings' layer, over its parent's.</summary>
    public static int LocalZIndex(Entity entity)
    {
        if (entity.Tags.Has<UiBackground>())
            return -1;

        // A dropdown floats above whatever it opens over, and its options above
        // it; nested content (a panel's text stacks its own z on its parent's)
        // must not draw over the list.
        if (entity.HasComponent<DropdownOption>())
            return 4;
        if (entity.Tags.Has<DropdownListPanel>())
            return 3;
        if (entity.Tags.Has<UiOverlayItem>())
            return 6;
        if (entity.Tags.Has<UiOverlay>())
            return 5;
        if (entity.Tags.Has<UiSliderFill>() || entity.Tags.Has<UiRaised>())
            return 2;

        return entity.HasComponent<Text>() ||
               entity.HasComponent<ButtonVisual>() ||
               entity.Tags.Has<TakesInput>()
            ? 1
            : 0;
    }
}

/// <summary>Not drawn, nor anything under it, while it is otherwise left alone (input, layout and systems still see it).</summary>
public struct CanvasHidden : ITag;
