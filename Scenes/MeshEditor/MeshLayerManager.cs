using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.GameDependencies;
using XanaduProject.Factories;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshLayerManager : IMeshLayerManager
    {
        private readonly EntityStore _entityStore;
        private readonly Rid _parentCanvasRid; // NEW: Store the parent CanvasRid
        private readonly List<Entity> _meshEntities = new();
        private int _activeMeshIndex = -1;
        private bool _showAllLayers = false;

        public Entity ActiveMesh => _activeMeshIndex >= 0 && _activeMeshIndex < _meshEntities.Count ? _meshEntities[_activeMeshIndex] : default;
        public int ActiveMeshIndex => _activeMeshIndex;

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public MeshLayerManager(EntityStore entityStore, Rid parentCanvasRid) // NEW: Accept parentCanvasRid
        {
            _entityStore = entityStore;
            _parentCanvasRid = parentCanvasRid; // NEW: Assign parentCanvasRid
            // Initialize with one default mesh layer
            AddNewMeshLayer();
            SetActiveMeshLayer(0);
        }

        public IReadOnlyList<string> GetMeshLayerNames()
        {
            return _meshEntities.Select(e => e.GetComponent<MeshComponent>().MeshData.Name).ToList();
        }

        public void AddNewMeshLayer()
        {
            MeshData newMeshData = new MeshData { Name = $"Mesh Layer {_meshEntities.Count}" };
            // NEW: Create RenderRid and assign it to MeshComponent
            Entity newMeshEntity = _entityStore.CreateEntity(new MeshComponent { MeshData = newMeshData, RenderRid = RenderRid.Create(_parentCanvasRid) });
            _meshEntities.Add(newMeshEntity);

            UpdateTriangulationForMesh(newMeshEntity.GetComponent<MeshComponent>()); // Pass MeshComponent

            // Set visibility for the new mesh's RenderRid
            if (!_showAllLayers && _meshEntities.Count > 1 && _activeMeshIndex != -1)
            {
                // If not showing all layers, and there's already an active mesh, hide the new one initially
                newMeshEntity.GetComponent<MeshComponent>().RenderRid.SetVisible(false);
            }
            else
            {
                // Otherwise, make it visible (either showing all, or it's the first/active mesh)
                newMeshEntity.GetComponent<MeshComponent>().RenderRid.SetVisible(true);
            }

            MeshLayersChanged?.Invoke();
            SetActiveMeshLayer(_meshEntities.Count - 1);
        }

        public void SetActiveMeshLayer(int index)
        {
            if (index >= 0 && index < _meshEntities.Count)
            {
                // Hide previously active mesh's RenderRid if not showing all layers
                if (!_showAllLayers && _activeMeshIndex != -1 && _activeMeshIndex < _meshEntities.Count)
                {
                    _meshEntities[_activeMeshIndex].GetComponent<MeshComponent>().RenderRid.SetVisible(false);
                }

                _activeMeshIndex = index;
                GD.Print($"Active Mesh Layer set to: {index}");

                // Show newly active mesh's RenderRid
                _meshEntities[_activeMeshIndex].GetComponent<MeshComponent>().RenderRid.SetVisible(true);

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
            if (_showAllLayers != showAll)
            {
                _showAllLayers = showAll;
                GD.Print($"Show All Layers set to: {showAll}");
                UpdateAllMeshTriangulations(); // Update all triangulations when show all layers changes

                // Update visibility of all RenderRids
                for (int i = 0; i < _meshEntities.Count; i++)
                {
                    var meshComponent = _meshEntities[i].GetComponent<MeshComponent>();
                    if (showAll)
                    {
                        meshComponent.RenderRid.SetVisible(true);
                    }
                    else
                    {
                        // If not showing all, only the active mesh should be visible
                        meshComponent.RenderRid.SetVisible(i == _activeMeshIndex);
                    }
                }

                MeshLayersChanged?.Invoke();
            }
        }

        public bool GetShowAllLayers()
        {
            return _showAllLayers;
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0 && index < _meshEntities.Count)
            {
                Entity entityToMove = _meshEntities[index];
                _meshEntities.RemoveAt(index);
                _meshEntities.Insert(index - 1, entityToMove);

                if (_activeMeshIndex == index) _activeMeshIndex = index - 1;
                else if (_activeMeshIndex == index - 1) _activeMeshIndex = index;

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
            if (index >= 0 && index < _meshEntities.Count - 1)
            {
                Entity entityToMove = _meshEntities[index];
                _meshEntities.RemoveAt(index);
                _meshEntities.Insert(index + 1, entityToMove);

                if (_activeMeshIndex == index) _activeMeshIndex = index + 1;
                else if (_activeMeshIndex == index + 1) _activeMeshIndex = index;

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
            return _meshEntities;
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
            foreach (var entity in _meshEntities)
            {
                if (entity != default)
                {
                    UpdateTriangulationForMesh(entity.GetComponent<MeshComponent>()); // Pass MeshComponent
                }
            }
        }
    }
}
