using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.Factories;
using XanaduProject.ECSComponents.EntitySystem.Components; // Import VisibleTag

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshLayerManager : IMeshLayerManager
    {
        private readonly EntityStore entityStore;
        private readonly Rid parentCanvasRid; // NEW: Store the parent CanvasRid
        private readonly List<Entity> meshEntities = new();
        private int activeMeshIndex = -1;
        // private bool _showAllLayers = false; // REMOVED: Replaced by VisibleTag

        public Entity ActiveMesh => activeMeshIndex >= 0 && activeMeshIndex < meshEntities.Count ? meshEntities[activeMeshIndex] : default;
        public int ActiveMeshIndex => activeMeshIndex;

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public MeshLayerManager(EntityStore entityStore, Rid parentCanvasRid) // NEW: Accept parentCanvasRid
        {
            this.entityStore = entityStore;
            this.parentCanvasRid = parentCanvasRid; // NEW: Assign parentCanvasRid
            // Initialize with one default mesh layer
            AddNewMeshLayer();
            SetActiveMeshLayer(0);
        }

        public IReadOnlyList<string> GetMeshLayerNames()
        {
            return meshEntities.Select(e => e.GetComponent<MeshComponent>().MeshData.Name).ToList();
        }

        public void AddNewMeshLayer()
        {
            MeshData newMeshData = new MeshData { Name = $"Mesh Layer {meshEntities.Count}" };
            // NEW: Create RenderRid and assign it to MeshComponent
            Entity newMeshEntity = entityStore.CreateEntity(new MeshComponent { MeshData = newMeshData, RenderRid = RenderRid.Create(parentCanvasRid) });

            meshEntities.Add(newMeshEntity);

            UpdateTriangulationForMesh(newMeshEntity.GetComponent<MeshComponent>()); // Pass MeshComponent

            // Set visibility for the new mesh using VisibleTag
            // By default, new layers are visible. If "show all layers" is off,
            // only the active layer should have the VisibleTag.
            // This logic will be handled by SetShowAllLayers and SetActiveMeshLayer.
            // For now, new layers are added with VisibleTag, and then adjusted by SetActiveMeshLayer.
            newMeshEntity.AddComponent(new VisibleTag());


            MeshLayersChanged?.Invoke();
            SetActiveMeshLayer(meshEntities.Count - 1);
        }

        public void SetActiveMeshLayer(int index)
        {
            if (index >= 0 && index < meshEntities.Count)
            {
                // Remove VisibleTag from previously active mesh
                if (activeMeshIndex != -1 && activeMeshIndex < meshEntities.Count)
                {
                    meshEntities[activeMeshIndex].RemoveComponent<VisibleTag>();
                }

                activeMeshIndex = index;
                GD.Print($"Active Mesh Layer set to: {index}");

                // Add VisibleTag to newly active mesh
                meshEntities[activeMeshIndex].AddComponent(new VisibleTag());

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
            if (ActiveMesh == default) return false;
            var meshData = ActiveMesh.GetComponent<MeshComponent>().MeshData;
            return meshData.SelectedBezierPointIndex != -1;
        }

        public bool GetActiveMeshHandlesLockedState()
        {
            if (ActiveMesh == default || !HasActiveMeshSelectedBezierPoint()) return false;
            var meshData = ActiveMesh.GetComponent<MeshComponent>().MeshData;
            return meshData.BezierPoints[meshData.SelectedBezierPointIndex].HandlesLocked;
        }

        public void SetActiveMeshHandlesLockedState(bool locked)
        {
            if (ActiveMesh == default || !HasActiveMeshSelectedBezierPoint()) return;
            var meshComponent = ActiveMesh.GetComponent<MeshComponent>(); // Get MeshComponent
            BezierPoint currentPoint = meshComponent.MeshData.BezierPoints[meshComponent.MeshData.SelectedBezierPointIndex];
            currentPoint.HandlesLocked = locked;
            if (locked)
            {
                currentPoint.OutHandle = -currentPoint.InHandle;
            }
            meshComponent.MeshData.BezierPoints[meshComponent.MeshData.SelectedBezierPointIndex] = currentPoint;
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
                    meshEntity.AddComponent(new VisibleTag());
                }
                else
                {
                    // If not showing all, only the active mesh should have the VisibleTag
                    if (i == activeMeshIndex)
                    {
                        meshEntity.AddComponent(new VisibleTag());
                    }
                    else
                    {
                        meshEntity.RemoveComponent<VisibleTag>();
                    }
                }
            }

            MeshLayersChanged?.Invoke();
        }

        public bool GetShowAllLayers()
        {
            // This method now reflects if all layers currently have the VisibleTag
            // or if only the active layer has it.
            if (meshEntities.Count == 0) return false; // No layers, so not showing all

            bool allVisible = true;
            foreach (var entity in meshEntities)
            {
                if (!entity.HasComponent<VisibleTag>())
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
            if (index >= 0 && index < meshEntities.Count - 1)
            {
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
            else
            {
                GD.PrintErr($"Attempted to move layer down from invalid index: {index}");
            }
        }

        public IReadOnlyList<Entity> GetAllMeshEntities()
        {
            return meshEntities;
        }


        // NEW: Triangulation methods
        public void UpdateTriangulationForMesh(MeshComponent meshComponent) // Accept MeshComponent
        {
            meshComponent.RenderRid.Clear(); // Clear the RenderRid before drawing
            meshComponent.MeshData.Triangles.Clear();

            if (meshComponent.MeshData.BezierPoints.Count >= 3)
            {
                // Generate sampled points from the Bezier curve for triangulation
                List<Vector2> sampledPoints = new List<Vector2>();
                int segmentsPerCurve = 10; // Number of linear segments to approximate each Bezier curve

                for (int i = 0; i < meshComponent.MeshData.BezierPoints.Count; i++)
                {
                    BezierPoint p1 = meshComponent.MeshData.BezierPoints[i];
                    BezierPoint p2 = meshComponent.MeshData.BezierPoints[(i + 1) % meshComponent.MeshData.BezierPoints.Count]; // Wrap around for closed curve

                    for (int j = 0; j < segmentsPerCurve; j++)
                    {
                        float t = (float)j / segmentsPerCurve;
                        Vector2 point = p1.Position.BezierInterpolate(p1.Position + p1.OutHandle, p2.Position + p2.InHandle, p2.Position, t);
                        sampledPoints.Add(point);
                    }
                }

                if (sampledPoints.Count >= 3)
                {
                    // Use TriangulatePolygon for correct polygon triangulation
                    int[]? indices = Geometry2D.TriangulatePolygon(sampledPoints.ToArray());
                    meshComponent.MeshData.Triangles.AddRange(indices);

                    // NEW: Draw the polygon using the RenderRid
                    meshComponent.RenderRid.AddTriangleArray(sampledPoints, meshComponent.MeshData.Triangles, Colors.White);
                }
            }
        }

        public void UpdateAllMeshTriangulations()
        {
            foreach (var entity in meshEntities)
            {
                if (entity != default)
                {
                    UpdateTriangulationForMesh(entity.GetComponent<MeshComponent>()); // Pass MeshComponent
                }
            }
        }
    }
}
