using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Character;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes
{
    public static class IkRiggingCharacterSetup
    {
        public static void Setup(Node parent)
        {
            var testCharacter = new TestCharacter();
            var testVisualsMaster = new TestVisualsMaster();

            DiProvider.Register(c =>
            {
                c.AddSingleton<IVisualsMaster>(testVisualsMaster);
                c.AddSingleton<IPlayerCharacter>(testCharacter);
            });

            parent.AddChild(testVisualsMaster);
            parent.AddChild(testCharacter);
        }
    }

    public partial class TestCharacter : Node2D, IPlayerCharacter
    {
        public RenderRid PlayerCanvasRid { get; }
        public MotionMachine MotionMachine { get; } = null!;

        public override void _Process(double delta)
        {
            base._Process(delta);
            Position = new Vector2(0, 60);
        }

        public void TriggerHold(float seconds) => throw new System.NotImplementedException();
        public void TriggerDirectedAcceleration(Direction direction) => throw new System.NotImplementedException();
        public void SnapToPosition(Vector2 worldPosition) => throw new System.NotImplementedException();

        public TestCharacter()
        {
            PlayerCanvasRid = GetCanvasItem().AsRenderRid();
        }
    }

    public partial class TestVisualsMaster : Node2D, IVisualsMaster
    {
        public CanvasLayer GameplayerLayer { get; }
        public Rid GameplayerLayerRid => GameplayerLayer.GetCanvas();
        public Vector2 CameraPosition { get; set; }

        public TestVisualsMaster()
        {
            GameplayerLayer = new CanvasLayer();
            AddChild(GameplayerLayer);
        }
    }
}
