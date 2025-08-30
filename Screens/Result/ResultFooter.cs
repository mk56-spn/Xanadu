// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Stage;
using XanaduProject.Tools;

namespace XanaduProject.Screens.Result
{
    public partial class ResultFooter : HBoxContainer
    {
        private readonly AnimatedHoverButton restart = new("Restart");
        private readonly AnimatedHoverButton menu = new("Go to menu");

        private readonly HBoxContainer buttons = new();
        public ResultFooter(ScreenManager manager, EntityStore store)
        {
            menu.Pressed += () =>manager.RequestChangeScreen(new MainMenu(), TransitionType.Fade);

            AddChild(buttons);
            buttons.AddChild(menu);
            buttons.AddChild(restart);


            restart.Pressed += () =>
            {
                var v = store.GetCommandBuffer();

                store.Query<NoteEcs>().ForEachEntity((ref NoteEcs _, Entity entity) =>
                {
                    v.RemoveComponent<Judged>(entity.Id);
                    v.RemoveComponent<Hit>(entity.Id);
                });
                manager.RequestChangeScreen(new Player(store,
                        GD.Load<TrackInfo>("res://Resources/TestTrack.tres"))
                    , TransitionType.Fade);

                v.Playback();
            };

            buttons.AddThemeConstantOverride("separation", 30);
        }

        public override void _Draw()
        {
            this.DrawTransitionLine(Size.X, 0, Size.Y -20, 120 ,Colors.Gold, 0.60f);

            this.DrawTransitionLine(Size.X, + 20, Size.Y + 3, 120 ,Colors.Gold, 0.6f, fill: true, fillColor:Colors.Gold);
        }

        public override void _Ready()
        {
            buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.KeepSize);
        }
    }
}
