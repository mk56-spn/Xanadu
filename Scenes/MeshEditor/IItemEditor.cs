using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;

namespace XanaduProject.Scenes.MeshEditor
{
    public interface IItemEditor
    {
        // The IItemEditor now primarily exposes the IMeshLayerManager
        IMeshLayerManager LayerManager { get; }

        // Events are proxied from the LayerManager for convenience
        event Action? MeshLayersChanged;
        event Action? ActiveMeshSelectionChanged;

        // NEW: Expose the CanvasItem's Rid for RenderRid parenting
        Rid CanvasRid { get; }
    }
}
