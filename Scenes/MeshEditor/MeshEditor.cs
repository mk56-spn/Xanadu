using Friflo.Engine.ECS;
using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshEditor : Control, IMeshEditor
    {
        public Entity MeshEntity { get; private set; }

        public MeshEditor()
        {
            var entityStore = DiProvider.Get<EntityStore>();
            MeshEntity = entityStore.CreateEntity(new MeshComponent { MeshData = new MeshData() });

            AddChild(new MeshEditorInput(this));
            AddChild(new MeshRenderSystem(this));
        }
    }
}
