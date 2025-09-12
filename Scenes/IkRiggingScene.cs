// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using System.Collections.Generic;
using System.Linq;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Scenes
{
    public partial class IkRiggingScene : Control
    {
        internal readonly ArchetypeQuery<IkTargetComponent> ikTargetsQuery;
        internal const float SELECTION_RADIUS = 20f;

        private readonly List<Entity> _ikTargetEntities = [];
        internal readonly List<Entity> allBones = [];

        internal int selectedPosePointIndex = -1;
        internal bool isStartPosePoint;

        internal readonly ArchetypeQuery<BoneEcs, BoneGlobalTransform> bonesQuery;
        internal readonly HashSet<Entity> ikBones = [];
        internal bool isRotatingBone;
        internal Entity selectedRotationBone;
        internal float grabStartAngle;
        internal float boneStartAngle;

        private readonly IkRiggingUI _ui;
        internal int selectedStartPoseIndex = -1;
        internal int selectedEndPoseIndex = -1;
        private readonly IkRiggingInputHandler _inputHandler;
        private readonly IkRiggingRenderer _renderer;
        private readonly IkRiggingEcsManager _ecsManager;

        public IkRiggingScene()
        {
            AddChild(new Camera2D());
            IkRiggingCharacterSetup.Setup(this);

            _ecsManager = new IkRiggingEcsManager();
            ikTargetsQuery = _ecsManager.GetIkTargetsQuery();
            bonesQuery = _ecsManager.GetBonesQuery();

            _ui = new IkRiggingUI();
            _ui.SliderValueChanged += OnSliderValueChanged;
            _ui.StartPoseSelected += OnStartPoseSelected;
            _ui.EndPoseSelected += OnEndPoseSelected;
            _ui.SavePosePressed += OnSavePosePressed;
            AddChild(_ui);

            _inputHandler = new IkRiggingInputHandler(this);
            _renderer = new IkRiggingRenderer(this);
        }

        public override void _Ready()
        {
            _ecsManager.SetupInitialScene(allBones, _ikTargetEntities);

            if (PoseIndex.POSES.Count == 0)
            {
                var startPose = new Pose { Name = "DefaultStart" };
                var endPose = new Pose { Name = "DefaultEnd" };

                foreach (var entity in _ikTargetEntities)
                {
                    var targetPos = entity.GetComponent<IkTargetComponent>().TargetPosition;
                    startPose.IkTargetPositions.Add(targetPos);
                    endPose.IkTargetPositions.Add(targetPos + new Vector2(100, 0));
                }

                var currentAngles = allBones.Select(b => b.GetComponent<BoneEcs>().Angle).ToList();
                startPose.BoneAngles = new List<float>(currentAngles);
                endPose.BoneAngles = new List<float>(currentAngles);

                PoseIndex.POSES.Add(startPose);
                PoseIndex.POSES.Add(endPose);
                PoseIndex.Save();
            }

            _ui.UpdatePoseDropdowns();

            if (PoseIndex.POSES.Count > 0) selectedStartPoseIndex = 0;
            if (PoseIndex.POSES.Count > 1) selectedEndPoseIndex = 1;

            OnSliderValueChanged(0);

            ikBones.Clear();
            foreach (var e in _ikTargetEntities)
            {
                ref var ik = ref e.GetComponent<IkTargetComponent>();
                ikBones.Add(ik.UpperBoneEntity);
                ikBones.Add(ik.LowerBoneEntity);
            }
        }

        internal void OnSliderValueChanged(double value)
        {
            if (selectedStartPoseIndex == -1 || selectedEndPoseIndex == -1 ||
                selectedStartPoseIndex >= PoseIndex.POSES.Count || selectedEndPoseIndex >= PoseIndex.POSES.Count)
            {
                return;
            }

            var startPose = PoseIndex.POSES[selectedStartPoseIndex];
            var endPose = PoseIndex.POSES[selectedEndPoseIndex];

            for (int i = 0; i < _ikTargetEntities.Count; i++)
            {
                if (i < startPose.IkTargetPositions.Count && i < endPose.IkTargetPositions.Count)
                {
                    ref var ikTarget = ref _ikTargetEntities[i].GetComponent<IkTargetComponent>();
                    ikTarget.TargetPosition = startPose.IkTargetPositions[i].Lerp(endPose.IkTargetPositions[i], (float)value);
                }
            }

            for (int i = 0; i < allBones.Count; i++)
            {
                if (i < startPose.BoneAngles.Count && i < endPose.BoneAngles.Count)
                {
                    var startAngle = startPose.BoneAngles[i];
                    var endAngle = endPose.BoneAngles[i];
                    if (float.IsNaN(startAngle) || float.IsNaN(endAngle)) continue;

                    ref var bone = ref allBones[i].GetComponent<BoneEcs>();
                    bone.Angle = Mathf.LerpAngle(startAngle, endAngle, (float)value);
                }
            }
        }

        public override void _Process(double delta)
        {
            _ecsManager.Update();
            QueueRedraw();
        }

        public override void _Draw() => _renderer.Draw();

        public override void _Input(InputEvent @event) => _inputHandler.HandleInput(@event);

        private void OnStartPoseSelected(long index)
        {
            selectedStartPoseIndex = (int)index;
            OnSliderValueChanged(_ui.GetSliderValue());
            QueueRedraw();
        }

        private void OnEndPoseSelected(long index)
        {
            selectedEndPoseIndex = (int)index;
            OnSliderValueChanged(_ui.GetSliderValue());
            QueueRedraw();
        }

        private void OnSavePosePressed(string poseName, Godot.Collections.Array<Vector2> ikTargetPositions, Godot.Collections.Array<float> boneAngles)
        {
            var currentPositions = _ikTargetEntities
                .Select(entity => entity.GetComponent<IkTargetComponent>().TargetPosition)
                .ToList();
            var currentAngles = allBones.Select(b => ikBones.Contains(b) ? float.NaN : b.GetComponent<BoneEcs>().Angle).ToList();

            PoseManager.SavePose(poseName, currentPositions, currentAngles);

            _ui.UpdatePoseDropdowns();
        }

        internal double GetSliderValue() => _ui.GetSliderValue();
        internal void SetSliderValue(double value) => _ui.SetSliderValue(value);
    }
}
