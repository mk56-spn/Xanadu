using System;
using Godot;
using XanaduProject.IO;

namespace XanaduProject.Scenes.ItemEditor
{
    public interface IItemEditor
    {
        IComponentLayerManager LayerManager { get; }

        Item CurrentItem { get; }

        event Action<Item> ItemSaved;

        event Action? LayersChanged;
        event Action? ActiveLayerSelectionChanged;

        Rid CanvasRid { get; }

        void TriggerSave();
    }
}
