using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Theme;

namespace Xanadu.Ui.Ecs.Ui;

public readonly partial struct UiNode
{
    /// <summary>
    /// A picker for long lists in categories (<see cref="UiTabbedPicker"/>), <paramref name="width"/>
    /// wide: a button reading the pick (<paramref name="empty"/> with none) that opens, in place under
    /// it, a grid of tabs (<paramref name="tabsPerRow"/> a row) over a pooled list
    /// <paramref name="rows"/> rows tall. Listen with <see cref="UiTabbedPickers.OnPicked"/>; set the
    /// pick with <see cref="UiTabbedPickers.Show"/>. Returns the picker's column.
    /// </summary>
    public UiNode TabbedPicker(IReadOnlyList<PickerTab> tabs, float width, string empty = "NONE",
        int rows = 8, int tabsPerRow = 4)
    {
        const float gap = UiMetrics.Space.Xxs;
        UiNode picker = Column().Width(width).Gap(UiMetrics.Space.Xs)
            .Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);
        picker.Button($"{empty} ▼", UiType.Caption).Size(width, UiMetrics.Control.Sm);

        UiNode body = picker.Column().Width(width).Gap(UiMetrics.Space.Xs)
            .Cross(StackCrossAlignment.Start).Main(StackMainAlignment.Start);
        int across = Math.Max(1, tabsPerRow);
        float tabWidth = (width - gap * (across - 1)) / across;
        UiNode strip = body.Grid(across, gap, gap);
        foreach (PickerTab tab in tabs)
            strip.Button(tab.Name, UiType.Caption).Size(tabWidth, UiMetrics.Control.Xs).Secondary();

        float rowHeight = UiMetrics.Control.Xs;
        UiNode list = body.List(rowHeight, gap, row => row.Button("", UiType.Caption).Height(rowHeight).Grow(1).Quiet())
            .Size(width, rows * (rowHeight + gap) - gap);

        Entity entity = picker.Entity;
        entity.AddComponent(new UiTabbedPicker([.. tabs], empty));
        list.Entity.OnChanged(() => UiTabbedPickers.Fill(entity));
        UiTabbedPickers.ShowTab(entity, 0);
        body.Hidden();
        return picker;
    }
}
