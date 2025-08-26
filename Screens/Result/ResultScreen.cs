// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Stage;

namespace XanaduProject.Screens.Result
{
    public partial class ResultScreen : Screen
    {
        private readonly VBoxContainer info = new();
        private readonly HBoxContainer buttons = new();
        private readonly AnimatedHoverButton restart = new("Restart");
        private readonly AnimatedHoverButton menu = new("Go to menu");
        public ResultScreen(EntityStore store)
        {
            info.AddChild(new ResultGraph(store));
            Color = Colors.Red;
            restart.Pressed += () =>
            {
                var v = store.GetCommandBuffer();

                store.Query<NoteEcs>().ForEachEntity((ref NoteEcs _, Entity entity) =>
                {
                    v.RemoveComponent<Judged>(entity.Id);
                    v.RemoveComponent<Hit>(entity.Id);
                });
                ScreenManager.RequestChangeScreen(new Player(store,
                        GD.Load<TrackInfo>("res://Resources/TestTrack.tres"))
                    , TransitionType.Fade);

                v.Playback();
            };

            menu.Pressed += () => ScreenManager.RequestChangeScreen(new MainMenu(), TransitionType.Fade);

            AddChild(buttons);
            buttons.AddChild(menu);
            buttons.AddChild(restart);

            buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            AddChild(info);
            info.AddThemeConstantOverride("separation", 20);
            buttons.AddThemeConstantOverride("separation", 30);

            info.AddChild(new ResultTally(store));
            AddChild(new ResultAccuracy(store));
        }
    }
}
