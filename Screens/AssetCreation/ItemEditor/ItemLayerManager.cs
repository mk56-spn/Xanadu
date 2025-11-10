using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.Scenes.ItemEditor;

namespace XanaduProject.Screens.AssetCreation.ItemEditor
{
    // This file should be renamed to ComponentLayerManager.cs
    public class ComponentLayerManager(EntityStore entityStore, Rid parentCanvasRid) : IComponentLayerManager
    {
        private readonly List<Entity> layerEntities = [];
        private int activeLayerIndex = -1;

        public Entity ActiveEntity => activeLayerIndex >= 0 && activeLayerIndex < layerEntities.Count ? layerEntities[activeLayerIndex] : default;
        public int ActiveLayerIndex => activeLayerIndex;

        public event Action? LayersChanged;
        public event Action? ActiveLayerSelectionChanged;

        public IReadOnlyList<string> GetLayerNames()
        {
            // TODO: This should come from a generic NameComponent, not MeshComponent.
            return layerEntities.Select(e => e.TryGetComponent(out Screens.AssetCreation.ItemEditor.MeshComponent mesh) ? mesh.Name : "Layer").ToList();
        }

        public Entity AddNewLayerEntity()
        {
            // For now, to maintain compatibility, we'll add a MeshComponent.
            // This should be refactored into a more generic system.
            var meshComponent = new Screens.AssetCreation.ItemEditor.MeshComponent { RenderRid = RenderRid.Create(parentCanvasRid) };
            Entity newEntity = entityStore.CreateEntity(meshComponent);
            AddLayerEntity(newEntity);
            return newEntity;
        }

        public void AddLayerEntity(Entity entity)
        {
            layerEntities.Add(entity);
            entity.AddTag<Visible>();

            LayersChanged?.Invoke();

            if (layerEntities.Count == 1)
            {
                SetActiveLayer(0);
            }
            else if (activeLayerIndex == -1)
            {
                SetActiveLayer(layerEntities.Count - 1);
            }
        }

        public void SetActiveLayer(int index)
        {
            if (index >= 0 && index < layerEntities.Count)
            {
                if (activeLayerIndex != -1 && activeLayerIndex < layerEntities.Count && !GetShowAllLayers())
                {
                    layerEntities[activeLayerIndex].RemoveTag<Visible>();
                }

                activeLayerIndex = index;
                layerEntities[activeLayerIndex].AddTag<Visible>();

                LayersChanged?.Invoke();
                ActiveLayerSelectionChanged?.Invoke();
            }
        }

        public void NotifySelectionChanged()
        {
            ActiveLayerSelectionChanged?.Invoke();
        }

        public void SetShowAllLayers(bool showAll)
        {
            for (int i = 0; i < layerEntities.Count; i++)
            {
                Entity layerEntity = layerEntities[i];
                if (showAll)
                {
                    layerEntity.AddTag<Visible>();
                }
                else
                {
                    if (i == activeLayerIndex)
                    {
                        layerEntity.AddTag<Visible>();
                    }
                    else
                    {
                        layerEntity.RemoveTag<Visible>();
                    }
                }
            }
            LayersChanged?.Invoke();
        }

        public bool GetShowAllLayers()
        {
            if (layerEntities.Count == 0) return false;
            return layerEntities.All(entity => entity.Tags.Has<Visible>());
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0 && index < layerEntities.Count)
            {
                Entity entityToMove = layerEntities[index];
                layerEntities.RemoveAt(index);
                layerEntities.Insert(index - 1, entityToMove);

                if (activeLayerIndex == index) activeLayerIndex = index - 1;
                else if (activeLayerIndex == index - 1) activeLayerIndex = index;

                LayersChanged?.Invoke();
                ActiveLayerSelectionChanged?.Invoke();
            }
        }

        public void MoveLayerDown(int index)
        {
            if (index < 0 || index >= layerEntities.Count - 1) return;

            Entity entityToMove = layerEntities[index];
            layerEntities.RemoveAt(index);
            layerEntities.Insert(index + 1, entityToMove);

            if (activeLayerIndex == index) activeLayerIndex = index + 1;
            else if (activeLayerIndex == index + 1) activeLayerIndex = index;

            LayersChanged?.Invoke();
            ActiveLayerSelectionChanged?.Invoke();
        }

        public IReadOnlyList<Entity> GetAllLayerEntities()
        {
            return layerEntities;
        }

        public void SetLayerColor(int layerIndex, Color color)
        {
            if (layerIndex < 0 || layerIndex >= layerEntities.Count) return;

            if (layerEntities[layerIndex].TryGetComponent(out Screens.AssetCreation.ItemEditor.MeshComponent meshComponent))
            {
                meshComponent.Color = color;
            }
        }

        public Color GetLayerColor(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < layerEntities.Count)
            {
                if (layerEntities[layerIndex].TryGetComponent(out Screens.AssetCreation.ItemEditor.MeshComponent meshComponent))
                {
                    return meshComponent.Color;
                }
            }
            return Colors.White;
        }
    }
}
