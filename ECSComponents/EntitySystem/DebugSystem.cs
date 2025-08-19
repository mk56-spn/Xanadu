// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.EcGui;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Character;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EcGuiSetup;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public partial class DebugSystem : QuerySystem
    {
        private readonly EntityStore store;
        private readonly Root root;
        private readonly PanelContainer panelContainer;

        private readonly EntityStore entityStore = DiProvider.Get<EntityStore>();

        public DebugSystem(EntityStore store, Root root)
        {
            this.store = store;
            this.root = root;

            panelContainer = new PanelContainer
            {
                OffsetLeft = 20,
                OffsetTop = 20,
                CustomMinimumSize = new Vector2(200, 500),
                ZIndex = 10,
            };

            panelContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.Black with { A = 0.5f },
                ContentMarginLeft = 10,
                ContentMarginBottom = 10,
                ContentMarginTop = 10,
                ContentMarginRight = 10,

                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,

                BorderColor = Colors.White,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderBlend = true
            });


            ecsViewer();
            setupContainer();


            DiProvider.Get<IUiMaster>().ScoreLayer.AddChild(panelContainer);

        }

        #region EcGui

        private void ecsViewer()
        {
            EcGui.AddExplorerStore("Store", entityStore);
            TypeDrawers.Register();
            EcGui.AddExplorerSystems(root);

            EcGui.Explorer.AddComponentMemberColumn<ElementEcs>(nameof(ElementEcs.Id));
            EcGui.Explorer.AddComponentMemberColumn<ElementEcs>(nameof(ElementEcs.Vector2));
            EcGui.Explorer.AddComponentMemberColumn<ActiveColourEcs>(nameof(ActiveColourEcs.Color));

            var elements = entityStore.Query<ElementEcs>();
            EcGui.AddExplorerQuery("elements", elements);
            var dormant = entityStore.Query<ElementEcs>().AllTags(Tags.Get<Dormant>());
            EcGui.AddExplorerQuery("dormant", dormant);
            var array = entityStore.Query<FloatArrayEcs>();
            EcGui.AddExplorerQuery("arrays", array);

        }

        #endregion

        #region Performance

        private void setupContainer()
        {
            var container = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
            panelContainer.AddChild(container);

             container.AddChild(new QueryLabel(store.Query<ElementEcs>(), "ELEMENTS :"));
            container.AddChild(new QueryLabel(store.Query().AllTags(Tags.Get<SelectionFlag>()), "SELECTED :"));
            container.AddChild(new QueryLabel(store.Query<NoteEcs>(), "Notes :"));
            container.AddChild(new QueryLabel(store.Query().AllComponents(ComponentTypes.Get<NoteEcs, Hit>()),
                "HIT NOTES :"));
            container.AddChild(new QueryLabel(store.Query().AllComponents(ComponentTypes.Get<FloatArrayEcs>()),
                "TRACKS :"));
            container.AddChild(new QueryLabel(store.Query().AllComponents(ComponentTypes.Get<CharacterEcs>()),
                "Characters :"));


            container.AddChild(new MethodLabel(() => Engine.GetFramesPerSecond(), "FPS :"));
            container.AddChild(new MethodLabel(() => container.GetTree().GetNodeCount(), "NODES :"));
            container.AddChild(new MethodLabel(
                () => Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame), "DRAW CALLS :"));
            container.AddChild(new MethodLabel(
                () => Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame), "PRIMITIVES :"));
            container.AddChild(new MethodLabel(
                () => Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame), "PRIMITIVES :"));

            container.AddChild(new MethodLabel(() => Performance.GetMonitor(Performance.Monitor.ObjectResourceCount),
                "RESOURCES :"));

            container.AddChild(new MethodLabel(()=> DiProvider.Get<IPlayerCharacter>().MotionMachine.State, "STATE :"));

            root.SetMonitorPerf(true);
            container.AddChild(new InputHandler(this));
        }

        #endregion

        private partial class DebugLabel : Label
        {
            public DebugLabel()
            {
                LabelSettings = new LabelSettings
                {
                    FontSize = 20,
                    Font = new FontVariation
                    {
                        BaseFont = GD.Load<Font>("uid://de4tk3tasbptp")
                    }
                };
            }
        }

        private partial class QueryLabel(ArchetypeQuery query, string staticText) : DebugLabel
        {
            private int lastCount = -1;

            public override void _Process(double delta){
                if (query.Count == lastCount) return;
                Text = staticText + query.Count;
                lastCount = query.Count;
            }
        }

        private partial class MethodLabel(Func<object> method, string staticText) : DebugLabel
        {
            private object  lastValue = null!;

            public override void _Ready()
            {
                updateText();
            }

            public override void _Process(double delta)
            {
                if (lastValue == method.Invoke()) return;
                updateText();
            }

            private void updateText()
            {
                lastValue = method.Invoke();
                Text = staticText + method.Invoke();
            }
        }

        private bool active;

        private partial class InputHandler(DebugSystem debug) : Node
        {
            public override void _Input(InputEvent @event)
            {
                base._Input(@event);

                if (@event is InputEventKey { Keycode: Key.F3, Pressed: true })

                    debug.active = !debug.active;
            }
        }

        protected override void OnUpdate()
        {
            panelContainer.Visible = active;
            if (!active) return;

            EcGui.ExplorerWindow();
            EcGui.InspectorWindow();
        }
    }
}
