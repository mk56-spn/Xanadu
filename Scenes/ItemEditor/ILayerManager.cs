using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.Scenes.ItemEditor
{
    // Note: This was renamed from ILayerManager
    public interface IComponentLayerManager
    {
        Entity ActiveEntity { get; }
        int ActiveLayerIndex { get; }
        event Action? LayersChanged;
        event Action? ActiveLayerSelectionChanged;

        IReadOnlyList<string> GetLayerNames();
        void SetActiveLayer(int index);

        // Creates a new entity with default components for a layer
        Entity AddNewLayerEntity();

        // Adds an existing entity to the layer management
        void AddLayerEntity(Entity entity);

        void NotifySelectionChanged();

        void SetShowAllLayers(bool showAll);
        bool GetShowAllLayers();

        void MoveLayerUp(int index);
        void MoveLayerDown(int index);
        IReadOnlyList<Entity> GetAllLayerEntities();

        void SetLayerColor(int layerIndex, Color color);
        Color GetLayerColor(int layerIndex);
    }
}
