// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Character;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.Scenes
{
    public partial class IkRiggingScene : Control
    {
        private readonly EntityStore entityStore;

        private readonly ArchetypeQuery<IkTargetComponent> ikTargetsQuery;
        private Entity? selectedTarget;
        private const float selection_radius = 20f;

        private readonly SystemRoot root;

        public IkRiggingScene()
        {
            AddChild( new Camera2D());
            TestCharacter test = new TestCharacter();
            TestVisualsMaster testVisualsMaster = new TestVisualsMaster();
            DiProvider.Register(c =>
            {
                c.AddSingleton<IVisualsMaster>(testVisualsMaster);
                c.AddSingleton<IPlayerCharacter>(test);
            }
                );

            AddChild(testVisualsMaster);
            AddChild(test);
            entityStore = new EntityStore();
            root = new SystemRoot();
            root.Add(new BoneTransformSystem(entityStore));

            root.Add(new IkSolverSystem(entityStore));
            root.Add(new BoneRenderingSystem());
            root.AddStore(entityStore);

            ikTargetsQuery = entityStore.Query<IkTargetComponent>();
        }

        public override void _Ready()
        {

            // Spine
            var shoulderEntity = entityStore.CreateEntity(
                new BoneEcs { Length = 20, Angle = float.Pi / 4 * 2.5f },
                new BoneGlobalTransform { GlobalPosition = new Vector2()}
            );


            var hipEntity = entityStore.CreateEntity(new BoneEcs { ParentEntity = shoulderEntity, Length = 50 });

            // Arms
            createLimb(shoulderEntity, new Vector2(-50, 64) + shoulderEntity.GetComponent<BoneGlobalTransform>().GlobalPosition, true, 35, 30);
            createLimb(shoulderEntity, new Vector2(-40, 70) + shoulderEntity.GetComponent<BoneGlobalTransform>().GlobalPosition, true, 35, 30);

            // Legs
            var hipInitialPos = shoulderEntity.GetComponent<BoneGlobalTransform>().GlobalPosition + new Vector2(0, 50);
            createLimb(hipEntity, new Vector2(0, 120) + hipInitialPos, false);
            createLimb(hipEntity, new Vector2(10, 130) + hipInitialPos, false);
        }

        private void createLimb(Entity parent, Vector2 targetPosition, bool bendUpwards, float lengthTop = 40, float lengthBottom = 35)
        {
            var upperLimb = entityStore.CreateEntity(new BoneEcs { Length = lengthTop, ParentEntity = parent });
            var lowerLimb = entityStore.CreateEntity(
                new BoneEcs { ParentEntity = upperLimb, Length = lengthBottom },
                new BoneGlobalTransform());

            entityStore.CreateEntity(new IkTargetComponent
            {
                UpperBoneEntity = upperLimb,
                LowerBoneEntity = lowerLimb,
                ElbowUp = bendUpwards,
                TargetPosition = targetPosition,
            });
        }

        public override void _Process(double delta)
        {
            root.Update(default);
            QueueRedraw();


        }

        public override void _Draw()
        {
            base._Draw();
            ikTargetsQuery.ForEachEntity((ref IkTargetComponent ikTarget, Entity entity) =>
            {
               DrawCircle( ikTarget.TargetPosition, 5, Colors.Red);
            });
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButtonEvent)
            {
                if (mouseButtonEvent.ButtonIndex == MouseButton.Left)
                {
                    if (mouseButtonEvent.Pressed)
                    {
                        // Find closest target
                        var mousePos = GetGlobalMousePosition();
                        ikTargetsQuery.ForEachEntity((ref IkTargetComponent ikTarget, Entity entity) =>
                        {
                            if (ikTarget.TargetPosition.DistanceTo(mousePos) < selection_radius)
                            {
                                selectedTarget = entity;
                            }
                        });
                    }
                    else
                    {
                        selectedTarget = null;
                    }
                }
            }

            if (@event is InputEventMouseMotion && selectedTarget.HasValue)
            {
                ref var ikTarget = ref selectedTarget.Value.GetComponent<IkTargetComponent>();
                ikTarget.TargetPosition = GetGlobalMousePosition();
            }
        }

        private partial class TestCharacter : Node2D, IPlayerCharacter
        {
            public RenderRid PlayerCanvasRid { get; }

            public MotionMachine MotionMachine { get; }

            public override void _Process(double delta)
            {
                base._Process(delta);
                Position = new Vector2(0, 60);

            }

            public void TriggerHold(float seconds)
            {
                throw new System.NotImplementedException();
            }

            public void TriggerDirectedAcceleration(Direction direction)
            {
                throw new System.NotImplementedException();
            }

            public void SnapToPosition(Vector2 worldPosition)
            {
                throw new System.NotImplementedException();
            }

            public TestCharacter()
            {PlayerCanvasRid = GetCanvasItem().AsRenderRid();
            }

        }

        private partial class TestVisualsMaster : Node2D, IVisualsMaster
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
}
