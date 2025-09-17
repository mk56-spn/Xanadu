using Friflo.Engine.ECS;
using System;
using System.Collections.Generic;

namespace XanaduProject.Scenes.MeshEditor
{
    public interface IMeshLayerManager
    {
        Entity ActiveMesh { get; }
        int ActiveMeshIndex { get; }
        event Action? MeshLayersChanged;
        event Action? ActiveMeshSelectionChanged;

        IReadOnlyList<string> GetMeshLayerNames();
        void SetActiveMeshLayer(int index);
        void AddNewMeshLayer();

        bool HasActiveMeshSelectedBezierPoint();
        bool GetActiveMeshHandlesLockedState();
        void SetActiveMeshHandlesLockedState(bool locked);
        void NotifyMeshSelectionChanged();

        void SetShowAllLayers(bool showAll);
        bool GetShowAllLayers();

        void MoveLayerUp(int index);
        void MoveLayerDown(int index);
        IReadOnlyList<Entity> GetAllMeshEntities();

        // CORRECTED: Triangulation methods now accept MeshComponent
        void UpdateTriangulationForMesh(MeshComponent meshComponent);
        void UpdateAllMeshTriangulations();
    }
}
