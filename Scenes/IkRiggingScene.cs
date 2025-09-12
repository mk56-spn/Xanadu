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
        internal readonly ArchetypeQuery<IkTargetComponent> IkTargetsQuery;
        internal const float SELECTION_RADIUS = 20f;

        private readonly List<Entity> ikTargetEntities = [];
        internal readonly List<Entity> AllBones = [];

        internal int SelectedPosePointIndex = -1;
        internal bool IsStartPosePoint;

        internal readonly ArchetypeQuery<BoneEcs, BoneGlobalTransform> BonesQuery;
        internal readonly HashSet<Entity> IkBones = [];
        internal bool IsRotatingBone;
        internal Entity SelectedRotationBone;
        internal float GrabStartAngle;
        internal float BoneStartAngle;

        private readonly IkRiggingUI ui;
        internal int SelectedStartPoseIndex = -1;
        internal int SelectedEndPoseIndex = -1;
        private readonly IkRiggingInputHandler inputHandler;
        private readonly IkRiggingRenderer renderer;
        private readonly IkRiggingEcsManager ecsManager;

        public IkRiggingScene()
        {
            AddChild(new Camera2D());
            IkRiggingCharacterSetup.Setup(this);

            ecsManager = new IkRiggingEcsManager();
            IkTargetsQuery = ecsManager.GetIkTargetsQuery();
            BonesQuery = ecsManager.GetBonesQuery();

            ui = new IkRiggingUI();
            ui.SliderValueChanged += OnSliderValueChanged;
            ui.StartPoseSelected += OnStartPoseSelected;
            ui.EndPoseSelected += OnEndPoseSelected;
            ui.SavePosePressed += OnSavePosePressed;
            AddChild(ui);

            inputHandler = new IkRiggingInputHandler(this);
            renderer = new IkRiggingRenderer(this);
        }

        public override void _Ready()
        {
            ecsManager.SetupInitialScene(AllBones, ikTargetEntities);

            if (PoseIndex.POSES.Count == 0)
            {
                var startPose = new Pose { Name = "DefaultStart" };
                var endPose = new Pose { Name = "DefaultEnd" };

                foreach (var entity in ikTargetEntities)
                {
                    var targetPos = entity.GetComponent<IkTargetComponent>().TargetPosition;
                    startPose.IkTargetPositions.Add(targetPos);
                    endPose.IkTargetPositions.Add(targetPos + new Vector2(100, 0));
                }

                var currentAngles = AllBones.Select(b => b.GetComponent<BoneEcs>().Angle).ToList();
                startPose.BoneAngles = new List<float>(currentAngles);
                endPose.BoneAngles = new List<float>(currentAngles);

                PoseIndex.POSES.Add(startPose);
                PoseIndex.POSES.Add(endPose);
                PoseIndex.Save();
            }

            ui.UpdatePoseDropdowns();

            if (PoseIndex.POSES.Count > 0) SelectedStartPoseIndex = 0;
            if (PoseIndex.POSES.Count > 1) SelectedEndPoseIndex = 1;

            OnSliderValueChanged(0);

            IkBones.Clear();
            foreach (var e in ikTargetEntities)
            {
                ref var ik = ref e.GetComponent<IkTargetComponent>();
                IkBones.Add(ik.UpperBoneEntity);
                IkBones.Add(ik.LowerBoneEntity);
            }
        }

        internal void OnSliderValueChanged(double value)
        {
            if (SelectedStartPoseIndex == -1 || SelectedEndPoseIndex == -1 ||
                SelectedStartPoseIndex >= PoseIndex.POSES.Count || SelectedEndPoseIndex >= PoseIndex.POSES.Count)
            {
                return;
            }

            var startPose = PoseIndex.POSES[SelectedStartPoseIndex];
            var endPose = PoseIndex.POSES[SelectedEndPoseIndex];

            for (int i = 0; i < ikTargetEntities.Count; i++)
            {
                if (i < startPose.IkTargetPositions.Count && i < endPose.IkTargetPositions.Count)
                {
                    ref var ikTarget = ref ikTargetEntities[i].GetComponent<IkTargetComponent>();
                    ikTarget.TargetPosition = startPose.IkTargetPositions[i].Lerp(endPose.IkTargetPositions[i], (float)value);
                }
            }

            for (int i = 0; i < AllBones.Count; i++)
            {
                if (i < startPose.BoneAngles.Count && i < endPose.BoneAngles.Count)
                {
                    float startAngle = startPose.BoneAngles[i];
                    float endAngle = endPose.BoneAngles[i];
                    if (float.IsNaN(startAngle) || float.IsNaN(endAngle)) continue;

                    ref var bone = ref AllBones[i].GetComponent<BoneEcs>();
                    bone.Angle = Mathf.LerpAngle(startAngle, endAngle, (float)value);
                }
            }
        }

        public override void _Process(double delta)
        {
            ecsManager.Update();
            QueueRedraw();
        }

        public override void _Draw() => renderer.Draw();

        public override void _Input(InputEvent @event) => inputHandler.HandleInput(@event);

        private void OnStartPoseSelected(long index)
        {
            SelectedStartPoseIndex = (int)index;
            OnSliderValueChanged(ui.GetSliderValue());
            QueueRedraw();
        }

        private void OnEndPoseSelected(long index)
        {
            SelectedEndPoseIndex = (int)index;
            OnSliderValueChanged(ui.GetSliderValue());
            QueueRedraw();
        }

        private void OnSavePosePressed(string poseName, Godot.Collections.Array<Vector2> ikTargetPositions, Godot.Collections.Array<float> boneAngles)
        {
            var currentPositions = ikTargetEntities
                .Select(entity => entity.GetComponent<IkTargetComponent>().TargetPosition)
                .ToList();
            var currentAngles = AllBones.Select(b => IkBones.Contains(b) ? float.NaN : b.GetComponent<BoneEcs>().Angle).ToList();

            PoseManager.SavePose(poseName, currentPositions, currentAngles);

            ui.UpdatePoseDropdowns();
        }

        internal double GetSliderValue() => ui.GetSliderValue();
        internal void SetSliderValue(double value) => ui.SetSliderValue(value);
    }
}
