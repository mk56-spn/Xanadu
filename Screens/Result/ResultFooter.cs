// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Stage;

namespace XanaduProject.Screens.Result
{
    public partial class ResultFooter : Footer
    {
        private readonly AnimatedHoverButton restart = new("Restart");
        private readonly AnimatedHoverButton menu = new("Go to menu");

        public ResultFooter(ScreenManager manager, Player oldPlayer)
        {
            menu.Pressed += () =>manager.RequestChangeScreen(new MainMenu(), TransitionType.Fade);
            AddButton(restart);
            AddButton(menu);


            restart.Pressed += () =>
            {
                var store = oldPlayer.EntityStore;
                var v = store.GetCommandBuffer();

                store.Query<NoteEcs>().ForEachEntity((ref NoteEcs _, Entity entity) =>
                {
                    v.RemoveComponent<Judged>(entity.Id);
                    v.RemoveComponent<Hit>(entity.Id);
                });
                manager.RequestChangeScreen(oldPlayer, TransitionType.Fade);

                v.Playback();
            };
        }

        public override void _Ready()
        {
            base._Ready();
            SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        }
    }
}
