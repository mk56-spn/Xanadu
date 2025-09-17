using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshLayerManager : IMeshLayerManager
    {
        private readonly EntityStore _entityStore;
        private readonly List<Entity> _meshEntities = new();
        private int _activeMeshIndex = -1;
        private bool _showAllLayers = false;

        public Entity ActiveMesh => _activeMeshIndex >= 0 && _activeMeshIndex < _meshEntities.Count ? _meshEntities[_activeMeshIndex] : default;
        public int ActiveMeshIndex => _activeMeshIndex;

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public MeshLayerManager(EntityStore entityStore)
        {
            _entityStore = entityStore;
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
            Entity newMeshEntity = _entityStore.CreateEntity(new MeshComponent { MeshData = newMeshData });
            _meshEntities.Add(newMeshEntity);

            MeshLayersChanged?.Invoke();
            SetActiveMeshLayer(_meshEntities.Count - 1);
        }

        public void SetActiveMeshLayer(int index)
        {
            if (index >= 0 && index < _meshEntities.Count)
            {
                _activeMeshIndex = index;
                GD.Print($"Active Mesh Layer set to: {index}");
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
            var meshData = ActiveMesh.GetComponent<MeshComponent>().MeshData;
            BezierPoint currentPoint = meshData.BezierPoints[meshData.SelectedBezierPointIndex];
            currentPoint.HandlesLocked = locked;
            if (locked)
            {
                currentPoint.OutHandle = -currentPoint.InHandle;
            }
            meshData.BezierPoints[meshData.SelectedBezierPointIndex] = currentPoint;
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
    }
}
