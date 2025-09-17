using Friflo.Engine.ECS;
using System;
using System.Collections.Generic;

namespace XanaduProject.Scenes.MeshEditor
{
    public interface IMeshEditor
    {
        // The IMeshEditor now primarily exposes the IMeshLayerManager
        IMeshLayerManager LayerManager { get; }

        // Events are proxied from the LayerManager for convenience
        event Action? MeshLayersChanged;
        event Action? ActiveMeshSelectionChanged;
    }
}
