using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Docs;
using Xanadu.Model.Docs;
using Xanadu.Screens.Docs;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;

namespace Xanadu.Wiring;

/// <summary>
/// The built-in docs, over whatever screen is up: built once on its own layer,
/// outside every scene, and shown or hidden by <see cref="DocsLogic.Toggle"/>
/// (handed in by <see cref="AppShortcutSystem"/>). Presses are heard as they
/// land and run on this system's update, where hiding the overlay is safe.
/// </summary>
public sealed class DocsOverlaySystem : BaseSystem, IDisposable
{
    private readonly DocsLogic _logic;
    private readonly DocsLayout _layout = new();
    private readonly UiNode _page;
    private readonly IDisposable _presses;
    private readonly Queue<(DocsEntrance Entrance, int Arg)> _pending = new();

    public DocsOverlaySystem(EntityStore entities)
    {
        _logic = new DocsLogic(DocShelf.Pages, Close);
        UiNode layer = UiNode.Layer(entities, default, UiLayers.Docs, HostPlatform.Visuals, UiStage.Full);
        _page = layer.Page().Hidden();
        _layout.Build(_page, _logic);
        ScreenBindings.Check(_page.Entity, _logic.Contract, _layout.Name);
        _presses = UiCommandEvents.OnPressed<DocsEntrance>(entities, (entrance, command, _) => _pending.Enqueue((entrance, command.Arg)));
    }

    public bool IsOpen => !UiLayout.IsHidden(_page.Entity);

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        _layout.Refresh(_logic);
        UiLayout.SetHidden(_page.Entity, false);
        _layout.DropIn(_page.Entity);
    }

    public void Close() => UiLayout.SetHidden(_page.Entity, true);

    protected override void OnUpdateGroup()
    {
        while (_pending.TryDequeue(out (DocsEntrance Entrance, int Arg) press))
            _logic.Do(press.Entrance, press.Arg);

        if (IsOpen)
            _layout.Refresh(_logic);
    }

    public void Dispose() => _presses.Dispose();
}
