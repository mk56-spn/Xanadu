using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.Scenes.ItemEditor
{
    public interface ILayerManager
    {
        Entity ActiveEntity { get; }
        int ActiveLayerIndex { get; }
        event Action? LayersChanged;
        event Action? ActiveLayerSelectionChanged;

        IReadOnlyList<string> GetLayerNames();
        void SetActiveLayer(int index);
        void AddNewMeshEntity(); // Will change later
        void AddMeshEntity(Screens.AssetCreation.ItemEditor.MeshComponent meshComponent); // Will change later

        bool HasActiveMeshSelectedBezierPoint();
        bool GetActiveMeshHandlesLockedState();
        void SetActiveMeshHandlesLockedState(bool locked);
        void NotifyMeshSelectionChanged(); // This seems generic enough.

        void SetShowAllLayers(bool showAll);
        bool GetShowAllLayers();

        void MoveLayerUp(int index);
        void MoveLayerDown(int index);
        IReadOnlyList<Entity> GetAllLayerEntities();

        // CORRECTED: Triangulation methods now accept MeshComponent
        void UpdateTriangulationForMesh(Screens.AssetCreation.ItemEditor.MeshComponent meshComponent);
        void UpdateAllMeshTriangulations();

        void SetLayerColor(int layerIndex, Color color);
        Color GetLayerColor(int layerIndex);
    }
}
