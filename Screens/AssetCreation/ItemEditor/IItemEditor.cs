using System;
using Godot;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    public interface IItemEditor
    {
        IComponentLayerManager LayerManager { get; }

        event Action? LayersChanged;
        event Action? ActiveLayerSelectionChanged;

        Rid CanvasRid { get; }

        Vector2 CanvasMousePosition { get; }

        float CanvasAngle { get; }

        void TriggerSave();
    }
}
