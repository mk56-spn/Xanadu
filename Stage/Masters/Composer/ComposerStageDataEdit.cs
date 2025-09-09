// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.IO;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class ComposerStageDataEdit : SubScreen
    {
        private VBoxContainer vbox;
        public ComposerStageDataEdit(StageData stageData)
        {
            Visible = true;

            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            StageInfo data = stageData.StageInfo;
            string oldStageName = data.StageName;


            vbox = new VBoxContainer();

            var songIndexLabel = new Label { Text = "Song Index" };
            var songIndexSpinBox = new SpinBox
            {
                Value = data.SongIndex,
                AllowGreater = true,
                AllowLesser = true,
                Step = 1
            };
            songIndexSpinBox.ValueChanged += value =>
            {
                data.SongIndex = (int)value;
                StagePersistence.SaveMetadata(stageData);
            };
            vbox.AddChild(songIndexLabel);
            vbox.AddChild(songIndexSpinBox);

            // StageName
            var stageNameLabel = new Label { Text = "StagePath Name" };
            var stageNameLineEdit = new LineEdit { Text = data.StageName };
            stageNameLineEdit.TextChanged += text =>
            {
                StagePersistence.RenameStage(oldStageName, text);
                data.StageName = text;
                oldStageName = text;
                StagePersistence.SaveMetadata(stageData);
            };
            vbox.AddChild(stageNameLabel);
            vbox.AddChild(stageNameLineEdit);

            // Difficulty
            var difficultyLabel = new Label { Text = "Difficulty" };
            var difficultySpinBox = new SpinBox
            {
                Value = data.Difficulty,
                AllowGreater = true,
                AllowLesser = true,
                Step = 1
            };
            difficultySpinBox.ValueChanged += value =>
            {
                data.Difficulty = (int)value;
                StagePersistence.SaveMetadata(stageData);
            };
            vbox.AddChild(difficultyLabel);
            vbox.AddChild(difficultySpinBox);

            // CreatorList
            var creatorListLabel = new Label { Text = "Creators (comma-separated)" };
            var creatorListLineEdit = new LineEdit { Text = string.Join(",", data.CreatorList) };
            creatorListLineEdit.TextChanged += text =>
            {
                data.CreatorList = text.Split(',');
                StagePersistence.SaveMetadata(stageData);
            };
            vbox.AddChild(creatorListLabel);
            vbox.AddChild(creatorListLineEdit);
        }
        public override void _Ready()
        {
            var color = new ColorRect{ Color = Colors.Black with { A = 0.5f } };
            color.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(color);
            AddChild(vbox);

            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        }
    }

}
