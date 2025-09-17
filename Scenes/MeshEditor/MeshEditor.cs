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
        public IMeshLayerManager LayerManager { get; private set; }

        public event Action? MeshLayersChanged;
        public event Action? ActiveMeshSelectionChanged;

        public Rid CanvasRid => GetCanvasItem(); // NEW: Implementation of CanvasRid

        public MeshEditor()
        {
            // Get the MeshLayerManager from the DiProvider
            // Pass the CanvasRid to the MeshLayerManager constructor
            LayerManager = new MeshLayerManager(DiProvider.Get<EntityStore>(), CanvasRid);

            // Proxy events from LayerManager
            LayerManager.MeshLayersChanged += () => MeshLayersChanged?.Invoke();
            LayerManager.ActiveMeshSelectionChanged += () => ActiveMeshSelectionChanged?.Invoke();

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            // Add other systems
            AddChild(new MeshEditorInput(this));
            AddChild(new MeshRenderNode(this));

            // Add the MeshLayerUI
            MeshEditorUi meshEditorUi = new MeshEditorUi(this);
            meshEditorUi.Position = new Vector2(10, 10); // Example position
            AddChild(meshEditorUi);
        }
    }
}
