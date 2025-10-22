using System;
using Godot;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public interface IItemEditor
    {
        IComponentLayerManager LayerManager { get; }

        Item CurrentItem { get; }

        event Action<Item> ItemSaved;

        event Action? LayersChanged;
        event Action? ActiveLayerSelectionChanged;

        Rid CanvasRid { get; }

        Vector2 CanvasTransform { get; }

        void TriggerSave();
    }
}
