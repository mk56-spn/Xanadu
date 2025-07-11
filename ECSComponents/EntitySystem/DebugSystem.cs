// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.EcGui;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public partial class DebugSystem: QuerySystem
    {
        private readonly PanelContainer panelContainer;
        public DebugSystem(CanvasLayer scoreLayer, EntityStore store, Root root)
        {

            panelContainer = new PanelContainer
            {
                OffsetLeft =  20,
                OffsetTop = 20,
                CustomMinimumSize = new Vector2(200,500)
            };

            panelContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.Black with { A = 0.5f },
                ContentMarginLeft = 10,
                ContentMarginBottom = 10,
                ContentMarginTop = 10,
                ContentMarginRight = 10,

                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,

                BorderColor = Colors.White,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderBlend = true
            });



            VBoxContainer container = new  VBoxContainer{ CustomMinimumSize = new Vector2(200, 0)};
            panelContainer.AddChild(container);

            scoreLayer.AddChild(panelContainer);

            container.AddChild(new QueryLabel(store.Query<ElementEcs>(), "ELEMENTS :"));
            container.AddChild(new QueryLabel(store.Query().AllTags(Tags.Get<SelectionFlag>()), "SELECTED :"));
            container.AddChild(new QueryLabel(store.Query<NoteEcs>(), "Notes :"));
            container.AddChild(new QueryLabel(store.Query().AllComponents(ComponentTypes.Get<NoteEcs, Hit>()), "HIT NOTES :"));
            container.AddChild(new QueryLabel(store.Query().AllComponents(ComponentTypes.Get<FloatArrayEcs>()), "TRACKS :"));



            container.AddChild(new MethodLabel(() => Engine.GetFramesPerSecond(), "FPS :"));
            container.AddChild(new MethodLabel(() => container.GetTree().GetNodeCount(), "NODES :"));
            container.AddChild(new MethodLabel(() => Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame), "DRAW CALLS :"));
            container.AddChild(new MethodLabel(() => Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame), "PRIMITIVES :"));
            container.AddChild(new MethodLabel(() => Performance.GetMonitor(Performance.Monitor.ObjectResourceCount), "RESOURCES :"));
            container.AddChild(new MethodLabel(() => Performance.GetMonitor(Performance.Monitor.ObjectResourceCount), "RESOURCES :"));
            container.AddChild(new MethodLabel(() => Performance.GetMonitor(Performance.Monitor.ObjectResourceCount), "RESOURCES :"));
            container.AddChild(new MethodLabel(() => root.MonitorPerf, "SYSTEMS :"));

            container.AddChild(new InputHandler(this));
        }

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
            public override void _Process(double delta)=> Text = staticText + query.Count;
        }

        private partial class MethodLabel(Func<object> method, string staticText) : DebugLabel
        {
            public override void _Process(double delta){
                Text = staticText + method.Invoke();
            }
        }

        private bool active;
        private partial class InputHandler(DebugSystem debug) : Node
        {
            public override void _Input(InputEvent @event)
            {
                base._Input(@event);

                if (@event is InputEventKey { Keycode: Key.F3 , Pressed: true })

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
