using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>
/// The drag-to-select gesture shared by every view that box-selects: it runs a
/// <see cref="UiMarquee{T}"/>, keeps the owner's <see cref="UiMarqueeBox"/> in
/// step for drawing, and applies the selection the box covers. A view derives
/// from it to say what lies under a box (<see cref="HitsIn"/>) and how a
/// selection is stored (<see cref="Select"/>); the playfield, the editor
/// timeline and the animator timeline all differ only in those two.
/// </summary>
/// <typeparam name="T">What the box selects.</typeparam>
public abstract class UiBoxSelector<T>(float threshold = 4f)
{
    private readonly UiMarquee<T> _marquee = new(threshold);

    /// <summary>Pressed on empty space; a drag past the threshold turns it into a box.</summary>
    public bool Armed => _marquee.Armed;

    /// <summary>A box is being dragged.</summary>
    public bool Active => _marquee.Active;

    /// <summary>Where the press happened. Kept after <see cref="End"/> so a click can act there.</summary>
    public Vector2 Start => _marquee.Start;

    /// <summary>A press that may become a box; a plain click if it never does.</summary>
    public void Arm(Vector2 start) => _marquee.Arm(start);

    /// <summary>Starts a box at once that keeps <paramref name="existing"/> selected.</summary>
    public void BeginAdditive(Vector2 start, IEnumerable<T> existing) =>
        _marquee.Begin(start, additive: true, existing);

    /// <summary>
    /// Follows the pointer. While a box is up it selects what the box covers
    /// and publishes the box on <paramref name="owner"/>; false if nothing is
    /// being dragged.
    /// </summary>
    public bool Drag(Entity owner, Vector2 pointer)
    {
        if (!_marquee.Update(pointer, out bool started))
            return false;

        if (started)
            OnStarted(owner);

        Publish(owner);
        Select(owner, _marquee.Selection(HitsIn(owner, _marquee.Bounds)));
        return true;
    }

    /// <summary>Finishes the press and hides the box.</summary>
    public MarqueeEnd End(Entity owner)
    {
        MarqueeEnd end = _marquee.End();
        Publish(owner);
        return end;
    }

    /// <summary>Drops any press or box without it counting as a click.</summary>
    public void Cancel(Entity owner) => End(owner);

    /// <summary>What a screen-space box covers.</summary>
    protected abstract IEnumerable<T> HitsIn(Entity owner, Rect2 bounds);

    /// <summary>Makes <paramref name="selection"/> the view's selection.</summary>
    protected abstract void Select(Entity owner, List<T> selection);

    /// <summary>A plain press just became a box, before it replaces the selection.</summary>
    protected virtual void OnStarted(Entity owner)
    {
    }

    private void Publish(Entity owner)
    {
        if (!owner.IsNull && owner.HasComponent<UiMarqueeBox>())
            _marquee.Publish(ref owner.GetComponent<UiMarqueeBox>());
    }
}
