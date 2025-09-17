using Friflo.Engine.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshEditor : Control, IMeshEditor
    {
        private readonly List<Entity> meshEntities = new();
        private int activeMeshIndex = -1;

        public Entity ActiveMesh => activeMeshIndex >= 0 && activeMeshIndex < meshEntities.Count ? meshEntities[activeMeshIndex] : default;
        public int ActiveMeshIndex => activeMeshIndex;

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public MeshEditor()
        {
            // Add other systems
            AddChild(new MeshEditorInput(this));
            AddChild(new MeshRenderNode(this));


            // Add the MeshLayerUI
            MeshEditorUi meshEditorUi = new MeshEditorUi(this);
            meshEditorUi.Position = new Vector2(10, 10); // Example position
            AddChild(meshEditorUi);

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
            var entityStore = DiProvider.Get<EntityStore>();
            MeshData newMeshData = new MeshData { Name = $"Mesh Layer {meshEntities.Count}" };
            Entity newMeshEntity = entityStore.CreateEntity(new MeshComponent { MeshData = newMeshData });
            meshEntities.Add(newMeshEntity);

            MeshLayersChanged?.Invoke(); // Notify UI that layers have changed
            SetActiveMeshLayer(meshEntities.Count - 1); // Automatically select the new layer
        }

        public void SetActiveMeshLayer(int index)
        {
            if (index >= 0 && index < meshEntities.Count)
            {
                activeMeshIndex = index;
                GD.Print($"Active Mesh Layer set to: {index}");
                MeshLayersChanged?.Invoke(); // Notify UI that active layer might have changed
                ActiveMeshSelectionChanged?.Invoke(); // Notify UI that active mesh selection might have changed (e.g., for handle lock state)
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
            // If locking, ensure handles are mirrored immediately
            if (locked)
            {
                currentPoint.OutHandle = -currentPoint.InHandle;
            }
            meshData.BezierPoints[meshData.SelectedBezierPointIndex] = currentPoint;
            // We don't need to call updateTriangulation here, as MeshEditorInput will handle it on drag/release
            // or MeshRenderNode will redraw on next frame.
            ActiveMeshSelectionChanged?.Invoke(); // Notify UI that handle lock state changed
        }

        // This method will be called by MeshEditorInput to signal a change in selection
        public void NotifyMeshSelectionChanged()
        {
            ActiveMeshSelectionChanged?.Invoke();
        }
    }
}
