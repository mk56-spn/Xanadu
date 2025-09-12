using Godot;
using System;

namespace XanaduProject.Scenes
{
    public partial class IkRiggingUI : Control
    {
        public event Action<double> SliderValueChanged;
        public event Action<long> StartPoseSelected;
        public event Action<long> EndPoseSelected;
        public event Action<string, Godot.Collections.Array<Vector2>, Godot.Collections.Array<float>> SavePosePressed;

        private HSlider _progressSlider;
        private OptionButton _startPoseDropdown;
        private OptionButton _endPoseDropdown;
        private LineEdit _newPoseNameEdit;
        private Button _savePoseButton;

        public IkRiggingUI()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            _progressSlider = new HSlider
                { MinValue = 0, MaxValue = 1, Step = 0.01f, Position = new Vector2(100, 10), Size = new Vector2(200, 20) };
            _progressSlider.ValueChanged += (value) => SliderValueChanged?.Invoke(value);
            AddChild(_progressSlider);

            _startPoseDropdown = new OptionButton { Position = new Vector2(100, 40), Size = new Vector2(150, 20) };
            _startPoseDropdown.ItemSelected += (index) => StartPoseSelected?.Invoke(index);
            AddChild(_startPoseDropdown);

            _endPoseDropdown = new OptionButton { Position = new Vector2(260, 40), Size = new Vector2(150, 20) };
            _endPoseDropdown.ItemSelected += (index) => EndPoseSelected?.Invoke(index);
            AddChild(_endPoseDropdown);

            _newPoseNameEdit = new LineEdit
                { Position = new Vector2(100, 70), Size = new Vector2(150, 20), PlaceholderText = "New Pose Name" };
            AddChild(_newPoseNameEdit);

            _savePoseButton = new Button { Text = "Save Current Pose", Position = new Vector2(260, 70) };
            _savePoseButton.Pressed += OnSavePosePressed;
            AddChild(_savePoseButton);
        }

        public void UpdatePoseDropdowns()
        {
            var startId = _startPoseDropdown.GetSelectedId();
            var endId = _endPoseDropdown.GetSelectedId();

            _startPoseDropdown.Clear();
            _endPoseDropdown.Clear();

            for (int i = 0; i < PoseIndex.POSES.Count; i++)
            {
                var poseName = PoseIndex.POSES[i].Name;
                _startPoseDropdown.AddItem(poseName, i);
                _endPoseDropdown.AddItem(poseName, i);
            }

            if (startId < PoseIndex.POSES.Count) _startPoseDropdown.Select(startId);
            if (endId < PoseIndex.POSES.Count) _endPoseDropdown.Select(endId);
        }

        private void OnSavePosePressed()
        {
            var poseName = _newPoseNameEdit.Text;
            // These arguments are placeholders, they will be provided by the main scene
            SavePosePressed?.Invoke(poseName, null, null);
            _newPoseNameEdit.Clear();
        }

        public double GetSliderValue()
        {
            return _progressSlider.Value;
        }

        public void SetSliderValue(double value)
        {
            _progressSlider.Value = value;
        }
    }
}
