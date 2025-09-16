using Friflo.Engine.ECS;
using Godot;
using Stateless;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes.MeshEditor
{
    public partial class MeshEditorInput : Control
    {
        private const float vertex_selection_radius = 10f;
        private const float drag_threshold = 5f;

        private readonly IMeshEditor editor;
        private readonly StateMachine<State, Trigger> stateMachine;
        private Vector2 pressPosition;

        private enum State { Idle, Pressed, Dragging }
        private enum Trigger { LeftDown, LeftUp, RightDown, MouseMoved }

        public MeshEditorInput(IMeshEditor editor)
        {
            this.editor = editor;

            stateMachine = new StateMachine<State, Trigger>(State.Idle);

            stateMachine.Configure(State.Idle)
                .Permit(Trigger.LeftDown, State.Pressed);



            stateMachine.Configure(State.Pressed)
                .OnEntry(HandlePress)
                .Permit(Trigger.LeftUp, State.Idle)
                .Permit(Trigger.MouseMoved, State.Dragging)
                .Permit(Trigger.RightDown, State.Idle);

            stateMachine.Configure(State.Dragging)
                .OnEntry(HandleDragStart)
                .Permit(Trigger.LeftUp, State.Idle)
                .Permit(Trigger.RightDown, State.Idle)
                .OnExit(HandleDragEnd);

            stateMachine.OnTransitioned(transition =>
            {
                if (transition.Trigger == Trigger.RightDown) HandleRightClick();
            });
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    stateMachine.Fire(mouseButton.Pressed ? Trigger.LeftDown : Trigger.LeftUp);
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
                {
                    stateMachine.Fire(Trigger.RightDown);
                }
            }
            else if (@event is InputEventMouseMotion && stateMachine.State == State.Pressed)
            {
                if (pressPosition.DistanceTo(GetGlobalMousePosition()) > drag_threshold)
                {
                    stateMachine.Fire(Trigger.MouseMoved);
                }
            }
        }

        private void HandlePress()
        {
            pressPosition = GetGlobalMousePosition();
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;

            int clickedVertex = -1;
            for (int i = 0; i < meshData.Vertices.Count; i++)
            {
                if (meshData.Vertices[i].DistanceTo(pressPosition) < vertex_selection_radius)
                {
                    clickedVertex = i;
                    break;
                }
            }

            if (clickedVertex != -1)
            {
                meshData.SelectedVertexIndex = clickedVertex;
            }
            else
            {
                // Add new vertex
                meshData.Vertices.Add(pressPosition);
                meshData.SelectedVertexIndex = meshData.Vertices.Count - 1;
            }
        }

        private void HandleRightClick()
        {
            var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;
            if (meshData.SelectedVertexIndex != -1)
            {
                meshData.Vertices.RemoveAt(meshData.SelectedVertexIndex);
                meshData.SelectedVertexIndex = -1;
            }
        }

        private void HandleDragStart()
        {
            // Logic to execute when dragging starts, if any.
        }

        public override void _Process(double delta)
        {
            if (stateMachine.State == State.Dragging)
            {
                var meshData = editor.MeshEntity.GetComponent<MeshComponent>().MeshData;                if (meshData.SelectedVertexIndex != -1)
                {
                    meshData.Vertices[meshData.SelectedVertexIndex] = GetGlobalMousePosition();
                }
            }
        }

        private void HandleDragEnd()
        {
            // Logic to execute when dragging ends, if any.
        }
    }
}
