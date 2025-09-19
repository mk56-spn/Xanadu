using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;
using XanaduProject.IO; // Added for Item

namespace XanaduProject.Scenes.MeshEditor
{
    public interface IItemEditor
    {
        IMeshLayerManager LayerManager { get; }

        Item CurrentItem { get; }

        event Action<Item> ItemSaved;

        event Action? MeshLayersChanged;
        event Action? ActiveMeshSelectionChanged;

        Rid CanvasRid { get; }

        // Triggers the save process.
        void TriggerSave();
    }
}
