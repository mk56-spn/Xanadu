using Friflo.Engine.ECS;
using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshRenderSystem(IMeshEditor editor) : Node2D
    {
        private readonly EntityStore entityStore = DiProvider.Get<EntityStore>();

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawCircle(Vector2.Zero, 1000, Colors.White);

            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;

            // Draw vertices
            for (int i = 0; i < meshData.Vertices.Count; i++)
            {
                var color = i == meshData.SelectedVertexIndex ? Colors.Red : Colors.White;
                DrawCircle(meshData.Vertices[i], 5, color);
            }
        }
    }
}
