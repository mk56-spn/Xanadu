using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Logic.Screens;
using Xanadu.Model.Ecs.Components;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;
using SceneState = Xanadu.Model.Ecs.Components.SceneState;

namespace Xanadu.Wiring.Scenes;

/// <summary>
/// Runs a player-facing screen split into logic and layout: makes the logic,
/// builds the chosen layout over it and checks the layout binds every mandatory
/// entrance, then each frame turns pressed entrances into
/// <see cref="IScreenLogic{T}.Do"/> calls and has the layout redraw. With more
/// than one layout it adds a switch that swaps them in place.
/// </summary>
public abstract class ScreenHost<TLogic, TEntrance> : ISceneController
    where TLogic : IScreenLogic<TEntrance>
    where TEntrance : struct, Enum
{
    private readonly ButtonPress _presses = new();
    private int _layoutIndex;
    private IScreenLayout<TLogic> _layout = null!;
    private IVisualItemFactory? _visuals;
    private bool _open;

    protected TLogic Logic { get; private set; } = default!;

    public abstract SceneKind Kind { get; }
    public virtual SceneBackgroundKind Background => SceneBackgroundKind.None;
    public virtual SceneKind BackTarget => SceneKind.None;

    /// <summary>A fresh logic for this visit to the screen.</summary>
    protected abstract TLogic CreateLogic(SceneBuildContext context, IScreenNavigator navigator);

    /// <summary>Every layout this screen has, the default first.</summary>
    protected abstract IScreenLayout<TLogic>[] Layouts();

    /// <summary>Whether the screen can open now; when not, it should send the player elsewhere.</summary>
    protected virtual bool CanOpen(SceneBuildContext context) => true;

    public void Build(SceneBuildContext context)
    {
        if (!CanOpen(context))
            return;

        Logic = CreateLogic(context, new Navigator(context.StateEntity, this));
        _visuals = context.Visuals;
        _open = true;
        BuildLayout(context.Entities, context.Root);
    }

    public virtual bool TryForward(SceneSystem system) => false;

    public void Leave()
    {
        if (!_open)
            return;

        _open = false;
        Logic.Close();
    }

    private void BuildLayout(EntityStore entities, Entity root)
    {
        IScreenLayout<TLogic>[] layouts = Layouts();
        _layout = layouts[_layoutIndex % layouts.Length];

        var layer = UiNode.Layer(entities, root, UiLayers.Menu, _visuals, UiStage.Full);
        _layout.Build(layer, Logic);
        ScreenBindings.Check(layer.Entity, Logic.Contract, _layout.Name);
        _layout.Refresh(Logic);

        if (layouts.Length > 1)
        {
            layer.Page().Add(new Button($"LAYOUT: {_layout.Name}", UiType.Small, tone: Tone.Quiet,
                cmd: Cmd(ScreenLayoutCommand.Next), tip: "Show the same screen in another layout")
            {
                Style = n => n.Anchor(UiEdge.TopRight, Space.Xl),
            });
        }
    }

    public void HandleActions(SceneSystem system, ref SceneState state)
    {
        if (!_open)
            return;

        Logic.Update();
        foreach ((Entity entity, TEntrance entrance, UiCommand command) in UiCommand.All<TEntrance>(system.Entities))
        {
            if (_presses.Started(entity))
                Logic.Do(entrance, command.Arg);
        }

        if (state.Pending != state.Current)
            return;

        foreach ((Entity entity, _, _) in UiCommand.All<ScreenLayoutCommand>(system.Entities))
        {
            if (!_presses.Started(entity))
                continue;

            _layoutIndex++;
            UiLayout.DeleteChildren(system.Root);
            BuildLayout(system.Entities, system.Root);
            return;
        }

        _layout.Refresh(Logic);
    }

    /// <summary>The game's side of <see cref="IScreenNavigator"/>: a scene request, or the screen's back target.</summary>
    private sealed class Navigator(Entity state, ISceneController screen) : IScreenNavigator
    {
        public void Go(SceneKind scene) => state.GetComponent<SceneState>().Pending = scene;

        public void Back()
        {
            if (screen.BackTarget != SceneKind.None)
                Go(screen.BackTarget);
        }
    }
}
