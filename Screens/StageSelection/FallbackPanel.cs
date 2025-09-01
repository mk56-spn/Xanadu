// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;

namespace XanaduProject.Screens.StageSelection
{
    public partial class FallbackPanel : Panel
    {
        private ScreenManager screen = DiProvider.Get<ScreenManager>();
        private LineEdit nameEdit = new();

        public FallbackPanel()
        {
            CustomMinimumSize = new Vector2(300, 300);
            var vbox = new VBoxContainer();
            AddChild(vbox);

            var label = new Label
            {
                Text = "No stages found. Create a new one?",
                LayoutMode = 1,
                AnchorsPreset = 8,
                GrowHorizontal = GrowDirection.Both,
                GrowVertical = GrowDirection.Both,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            vbox.AddChild(label);

            nameEdit.Text = "New Level";
            vbox.AddChild(nameEdit);

            var button = new Button
            {
                Text = "Create",
                LayoutMode = 1,
                AnchorsPreset = 7,
                GrowHorizontal = GrowDirection.Both,
                GrowVertical = GrowDirection.Begin
            };
            vbox.AddChild(button);

            button.Pressed += () =>
            {
                var stageData = StagePersistence.LoadCleanStage(nameEdit.Text);
                StagePersistence.SaveStage(stageData);
                StagePersistence.SaveMetadata(stageData);

                // Switch to the composer
                screen.RequestChangeScreen(() =>
                    new Stage.Masters.Composer.Composer(stageData));
            };
        }
    }
}
