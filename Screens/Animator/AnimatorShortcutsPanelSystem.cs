using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Logic.Animator;
using Xanadu.Model.Animator;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Screens.Animator;

// Reads the pointer outside UI (BannedSymbols.txt): same as the FILE INFO popup, still to port.
#pragma warning disable RS0030

/// <summary>Centres the SHORTCUTS sheet over its dimmer while open; a click outside closes it.</summary>
public sealed class AnimatorShortcutsPanelSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorUi>
{
    private bool _primaryHeld;
    private bool _wasOpen;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = AnimatorPointerEdge.Primary(ref _primaryHeld, input);

        foreach (Entity session in Query.ToEntityList())
        {
            AnimatorUi ui = session.GetComponent<AnimatorUi>();
            if (ui.ShortcutsPanel.IsNull || ui.ShortcutsBackdrop.IsNull)
                continue;

            Entity panel = ui.ShortcutsPanel;
            bool justOpened = panel.IsOpen() && !_wasOpen;
            if (panel.IsOpen() && primary && !justOpened &&
                !AnimatorGeometry.PanelArea(ui.ShortcutsPanel).HasPoint(input.Position))
            {
                panel.SetOpen(false);
            }

            _wasOpen = panel.IsOpen();
            if (!panel.IsOpen())
            {
                AnimatorMenuPlacement.Hide(ui.ShortcutsBackdrop);
                AnimatorMenuPlacement.Hide(ui.ShortcutsPanel);
                continue;
            }

            Vector2 size = AnimatorLayout.ShortcutsPanelSize;
            AnimatorMenuPlacement.Place(ui.ShortcutsBackdrop, ui.ScreenSize / 2);
            AnimatorMenuPlacement.Place(ui.ShortcutsPanel, (ui.ScreenSize - size) / 2, size);
        }
    }
}
