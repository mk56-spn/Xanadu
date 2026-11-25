using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Runs tabbed pickers (<see cref="UiTabbedPicker"/>): the button opens and closes the
/// body (on the pick's tab), a tab lists its entries, and an entry picks itself,
/// closes the body and signals <see cref="PickerPicked"/>.
/// </summary>
public sealed class UiTabbedPickerSystem : QuerySystem<UiTabbedPicker>
{
    private readonly ButtonPress _presses = new();
    private readonly List<(Entity Picker, int Tab, int Entry)> _picked = [];

    protected override void OnUpdate()
    {
        // Pickers are few (one per open editor), and a press changes structure.
        foreach (Entity picker in Query.ToEntityList())
        {
            if (_presses.Started(UiTabbedPickers.ButtonOf(picker)))
                Toggle(picker);
            if (!UiTabbedPickers.IsOpen(picker))
                continue;

            Entity tabs = UiTabbedPickers.TabsOf(picker);
            for (int i = 0; i < tabs.ChildCount; i++)
            {
                if (_presses.Started(tabs.ChildEntities[i]))
                    UiTabbedPickers.ShowTab(picker, i);
            }

            UiList view = UiTabbedPickers.ListOf(picker).GetComponent<UiList>();
            for (int i = 0; i < view.Shown; i++)
            {
                if (!_presses.Started(UiTabbedPickers.EntryOf(picker, i)))
                    continue;
                int tab = picker.GetComponent<UiTabbedPicker>().Tab;
                UiTabbedPickers.Show(picker, tab, view.First + i);
                UiLayout.SetHidden(UiTabbedPickers.BodyOf(picker), true);
                _picked.Add((picker, tab, view.First + i));
                break;
            }
        }

        // Listeners run last: a handler may rebuild its panel.
        foreach ((Entity picker, int tab, int entry) in _picked)
        {
            if (!picker.IsNull)
                picker.EmitSignal(new PickerPicked(tab, entry));
        }
        _picked.Clear();
    }

    private static void Toggle(Entity picker)
    {
        bool open = !UiTabbedPickers.IsOpen(picker);
        UiLayout.SetHidden(UiTabbedPickers.BodyOf(picker), !open);
        if (open)
            UiTabbedPickers.ShowTab(picker, Math.Max(0, picker.GetComponent<UiTabbedPicker>().PickedTab));
    }
}
