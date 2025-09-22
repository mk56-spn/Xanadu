using System;
using Godot;
using XanaduProject.Scenes;

namespace XanaduProject.Screens.AssetCreation
{
    public partial class AssetCreationUi : Control
    {
        public event Action<double>? SliderValueChanged;
        public event Action<long>? StartPoseSelected;
        public event Action<long>? EndPoseSelected;
        public event Action<string, Godot.Collections.Array<Vector2>, Godot.Collections.Array<float>>? SavePosePressed;

        private HSlider progressSlider;
        private OptionButton startPoseDropdown;
        private OptionButton endPoseDropdown;
        private LineEdit newPoseNameEdit;
        private Button savePoseButton;

        public AssetCreationUi()
        {
            setupUi();
        }

        private void setupUi()
        {
            progressSlider = new HSlider
                { MinValue = 0, MaxValue = 1, Step = 0.01f, Position = new Vector2(100, 10), Size = new Vector2(200, 20) };
            progressSlider.ValueChanged += (value) => SliderValueChanged?.Invoke(value);
            AddChild(progressSlider);

            startPoseDropdown = new OptionButton { Position = new Vector2(100, 40), Size = new Vector2(150, 20) };
            startPoseDropdown.ItemSelected += (index) => StartPoseSelected?.Invoke(index);
            AddChild(startPoseDropdown);

            endPoseDropdown = new OptionButton { Position = new Vector2(260, 40), Size = new Vector2(150, 20) };
            endPoseDropdown.ItemSelected += (index) => EndPoseSelected?.Invoke(index);
            AddChild(endPoseDropdown);

            newPoseNameEdit = new LineEdit
                { Position = new Vector2(100, 70), Size = new Vector2(150, 20), PlaceholderText = "New Skeleton Name" };
            AddChild(newPoseNameEdit);

            savePoseButton = new Button { Text = "Save Current Skeleton", Position = new Vector2(260, 70) };
            savePoseButton.Pressed += OnSavePosePressed;
            AddChild(savePoseButton);
        }

        public void UpdatePoseDropdowns()
        {
            int startId = startPoseDropdown.GetSelectedId();
            int endId = endPoseDropdown.GetSelectedId();

            startPoseDropdown.Clear();
            endPoseDropdown.Clear();

            for (int i = 0; i < PoseIndex.POSES.Count; i++)
            {
                string poseName = PoseIndex.POSES[i].Name;
                startPoseDropdown.AddItem(poseName, i);
                endPoseDropdown.AddItem(poseName, i);
            }

            if (startId < PoseIndex.POSES.Count) startPoseDropdown.Select(startId);
            if (endId < PoseIndex.POSES.Count) endPoseDropdown.Select(endId);
        }

        private void OnSavePosePressed()
        {
            string? poseName = newPoseNameEdit.Text;
            // These arguments are placeholders, they will be provided by the main scene
            SavePosePressed?.Invoke(poseName, null, null);
            newPoseNameEdit.Clear();
        }

        public double GetSliderValue()
        {
            return progressSlider.Value;
        }

        public void SetSliderValue(double value)
        {
            progressSlider.Value = value;
        }
    }
}
