using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Scenes;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// Dragging the two scrollbars on song select: the level list's moves the
/// selection, the scores' scrolls their viewport. Presses come through each
/// track's Pressed tag, so anything drawn over a track takes them instead.
/// </summary>
public sealed class SongSelectScrollbarSystem(IPointerInputSource pointer, SceneSystem scenes)
    : QuerySystem<SongSelectState>
{
    private enum Bar
    {
        None,
        List,
        Scores,
    }

    private Bar _bar;
    private float _grab;

    protected override void OnUpdate()
    {
        if (Query.Count == 0)
        {
            _bar = Bar.None;
            return;
        }

        foreach (Entity root in Query.ToEntityList())
        {
            float y = pointer.Read().Position.Y;
            SongSelectState state = root.GetComponent<SongSelectState>();
            UpdateBar(Bar.List, state.ListTrack, SongSelectScene.ListSlider(state), SongSelectScene.ListValue(state), y);
            UpdateBar(Bar.Scores, state.ScoreTrack, SongSelectScene.ScoreSlider(state), SongSelectScene.ScoreValue(state), y);
            if (_bar == Bar.List)
                DragList(state, y);
            else if (_bar == Bar.Scores)
                SongSelectScene.SetScoreValue(state, SongSelectScene.ScoreSlider(state).ValueAt(y, _grab));
        }
    }

    /// <summary>Takes hold of a bar when its track is pressed and lets go when the press ends.</summary>
    private void UpdateBar(Bar bar, Entity track, VerticalSlider slider, float value, float y)
    {
        bool pressed = !track.IsNull && track.Tags.Has<Pressed>();
        if (_bar == bar && !pressed)
            _bar = Bar.None;
        else if (_bar == Bar.None && pressed && slider.CanSlide)
        {
            _bar = bar;
            _grab = slider.Grab(y, value);
        }
    }

    private void DragList(SongSelectState state, float y)
    {
        int last = state.Catalog.Count - 1;
        int wanted = Mathf.RoundToInt(SongSelectScene.ListSlider(state).ValueAt(y, _grab) * last);
        if (wanted != state.Selected)
            scenes.TryStepCarousel(wanted - state.Selected);
    }
}
