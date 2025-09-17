using Friflo.Engine.ECS;
using System;
using System.Collections.Generic;

namespace XanaduProject.Scenes.MeshEditor
{
    public interface IMeshEditor
    {
        Entity ActiveMesh { get; }
        int ActiveMeshIndex { get; }
        event Action MeshLayersChanged;
        IReadOnlyList<string> GetMeshLayerNames();
        void SetActiveMeshLayer(int index);
        void AddNewMeshLayer();

        // New methods for handle locking
        bool HasActiveMeshSelectedBezierPoint();
        bool GetActiveMeshHandlesLockedState();
        void SetActiveMeshHandlesLockedState(bool locked);
        event Action ActiveMeshSelectionChanged; // To notify UI when selection changes
        void NotifyMeshSelectionChanged(); // Added this method
    }
}
