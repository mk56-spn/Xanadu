using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.Factories;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Scenes.MeshEditor
{
    public class ItemLayerManager(EntityStore entityStore, Rid parentCanvasRid) : IMeshLayerManager
    {
        private readonly List<Entity> meshEntities = [];
        private int activeMeshIndex = -1;

        public Entity ActiveEntity => activeMeshIndex >= 0 && activeMeshIndex < meshEntities.Count ? meshEntities[activeMeshIndex] : default;
        public int ActiveMeshIndex => activeMeshIndex;

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        // NEW: Accept parentCanvasRid

        public IReadOnlyList<string> GetMeshLayerNames()
        {
            return meshEntities.Select(e => e.GetComponent<MeshComponent>().Name).ToList();
        }

        public void AddNewMeshEntity()
        {
            AddMeshEntity(new MeshComponent()); // Default color for new layers
        }


        public void AddMeshEntity(MeshComponent meshComponent)
        {
            Entity newMeshEntity = entityStore.CreateEntity(new MeshComponent {  RenderRid = RenderRid.Create(parentCanvasRid)});

            meshEntities.Add(newMeshEntity);

            UpdateTriangulationForMesh(newMeshEntity.GetComponent<MeshComponent>());

            // Set visibility for the new mesh using Visible
            newMeshEntity.AddTag<Visible>();

            MeshLayersChanged?.Invoke();

            // If this is the first layer added, make it active
            if (meshEntities.Count == 1)
            {
                SetActiveMeshLayer(0);
            }
            else if (activeMeshIndex == -1) // If no active layer is set yet, set the last added as active
            {
                SetActiveMeshLayer(meshEntities.Count - 1);
            }
        }

        public void SetActiveMeshLayer(int index)
        {
            if (index >= 0 && index < meshEntities.Count)
            {
                // Remove Visible from previously active mesh
                if (activeMeshIndex != -1 && activeMeshIndex < meshEntities.Count)
                {
                    meshEntities[activeMeshIndex].RemoveTag<Visible>();
                }

                activeMeshIndex = index;
                GD.Print($"Active Mesh Layer set to: {index}");

                // Add Visible to newly active mesh
                meshEntities[activeMeshIndex].AddTag<Visible>();

                MeshLayersChanged?.Invoke();
                ActiveMeshSelectionChanged?.Invoke();
            }
            else
            {
                GD.PrintErr($"Attempted to set active mesh layer to invalid index: {index}");
            }
        }

        public bool HasActiveMeshSelectedBezierPoint()
        {
            if (ActiveEntity == default) return false;
            ref var meshData = ref ActiveEntity.GetComponent<MeshComponent>();
            return meshData.SelectedBezierPointIndex != -1;
        }

        public bool GetActiveMeshHandlesLockedState()
        {
            if (ActiveEntity == default || !HasActiveMeshSelectedBezierPoint()) return false;
            var meshData = ActiveEntity.GetComponent<MeshComponent>();
            return meshData.BezierPoints[meshData.SelectedBezierPointIndex].HandlesLocked;
        }

        public void SetActiveMeshHandlesLockedState(bool locked)
        {
            if (ActiveEntity == default || !HasActiveMeshSelectedBezierPoint()) return;
            var meshComponent = ActiveEntity.GetComponent<MeshComponent>(); // Get MeshComponent
            BezierPoint currentPoint = meshComponent.BezierPoints[meshComponent.SelectedBezierPointIndex];
            currentPoint.HandlesLocked = locked;
            if (locked)
            {
                currentPoint.OutHandle = -currentPoint.InHandle;
            }
            meshComponent.BezierPoints[meshComponent.SelectedBezierPointIndex] = currentPoint;
            UpdateTriangulationForMesh(meshComponent); // Pass MeshComponent
            ActiveMeshSelectionChanged?.Invoke();
        }

        public void NotifyMeshSelectionChanged()
        {
            ActiveMeshSelectionChanged?.Invoke();
        }

        public void SetShowAllLayers(bool showAll)
        {
            // The _showAllLayers field is removed, so we directly apply the visibility logic
            GD.Print($"Show All Layers set to: {showAll}");
            UpdateAllMeshTriangulations(); // Update all triangulations when show all layers changes

            for (int i = 0; i < meshEntities.Count; i++)
            {
                Entity meshEntity = meshEntities[i];
                if (showAll)
                {
                    meshEntity.AddTag<Visible>();
                }
                else
                {
                    // If not showing all, only the active mesh should have the Visible
                    if (i == activeMeshIndex)
                    {
                        meshEntity.AddTag<Visible>();
                    }
                    else
                    {
                        meshEntity.RemoveTag<Visible>();
                    }
                }
            }

            MeshLayersChanged?.Invoke();
        }

        public bool GetShowAllLayers()
        {
            // This method now reflects if all layers currently have the Visible
            // or if only the active layer has it.
            if (meshEntities.Count == 0) return false; // No layers, so not showing all

            bool allVisible = true;
            foreach (var entity in meshEntities)
            {
                if (!entity.Tags.Has<Visible>())
                {
                    allVisible = false;
                    break;
                }
            }
            return allVisible;
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0 && index < meshEntities.Count)
            {
                Entity entityToMove = meshEntities[index];
                meshEntities.RemoveAt(index);
                meshEntities.Insert(index - 1, entityToMove);

                // Adjust active mesh index if the moved layer or the layer it swapped with was active
                if (activeMeshIndex == index) activeMeshIndex = index - 1;
                else if (activeMeshIndex == index - 1) activeMeshIndex = index;

                GD.Print($"Moved layer {index} up to {index - 1}");
                UpdateAllMeshTriangulations(); // Update all triangulations after reordering
                MeshLayersChanged?.Invoke();
                ActiveMeshSelectionChanged?.Invoke();
            }
            else
            {
                GD.PrintErr($"Attempted to move layer up from invalid index: {index}");
            }
        }

        public void MoveLayerDown(int index)
        {
            if (index < 0 || index >= meshEntities.Count - 1) return;

            Entity entityToMove = meshEntities[index];
            meshEntities.RemoveAt(index);
            meshEntities.Insert(index + 1, entityToMove);

            // Adjust active mesh index if the moved layer or the layer it swapped with was active
            if (activeMeshIndex == index) activeMeshIndex = index + 1;
            else if (activeMeshIndex == index + 1) activeMeshIndex = index;

            GD.Print($"Moved layer {index} down to {index + 1}");
            UpdateAllMeshTriangulations(); // Update all triangulations after reordering
            MeshLayersChanged?.Invoke();
            ActiveMeshSelectionChanged?.Invoke();
        }

        public IReadOnlyList<Entity> GetAllMeshEntities()
        {
            return meshEntities;
        }


        // NEW: Triangulation methods
        public void UpdateTriangulationForMesh(MeshComponent meshComponent) // Accept MeshComponent
        {
            meshComponent.RenderRid.Clear(); // Clear the RenderRid before drawing

            if (meshComponent.BezierPoints.Count < 3) return;

            var (vertices, indices) = BezierTriangulator.Triangulate(meshComponent.BezierPoints);

            if (vertices.Count < 3 || indices.Count <= 0) return;

            // NEW: Draw the polygon using the RenderRid
            meshComponent.RenderRid.AddTriangleArray(vertices, indices, meshComponent.Color);
        }

        public void UpdateAllMeshTriangulations()
        {
            foreach (var entity in meshEntities.Where(entity => entity != default))
            {
                UpdateTriangulationForMesh(entity.GetComponent<MeshComponent>()); // Pass MeshComponent
            }
        }

        public void SetLayerColor(int layerIndex, Color color)
        {
            if (layerIndex < 0 || layerIndex >= meshEntities.Count) return;

            ref var meshComponent = ref meshEntities[layerIndex].GetComponent<MeshComponent>();
            meshComponent.Color = color;
            UpdateTriangulationForMesh(meshComponent);
        }

        public Color GetLayerColor(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < meshEntities.Count)
            {
                return meshEntities[layerIndex].GetComponent<MeshComponent>().Color;
            }
            return Colors.White;
        }
    }
}
