using Godot;
using System.Linq;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Scenes
{
    public class IkRiggingInputHandler
    {
        private readonly IkRiggingScene _scene;

        public IkRiggingInputHandler(IkRiggingScene scene)
        {
            _scene = scene;
        }

        public void HandleInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButtonEvent)
            {
                HandleMouseButton(mouseButtonEvent);
            }
            else if (@event is InputEventMouseMotion mouseMotionEvent)
            {
                HandleMouseMotion(mouseMotionEvent);
            }
        }

        private void HandleMouseButton(InputEventMouseButton mouseButtonEvent)
        {
            if (mouseButtonEvent.ButtonIndex != MouseButton.Left) return;

            if (mouseButtonEvent.Pressed)
            {
                HandleLeftMousePressed(_scene.GetGlobalMousePosition());
            }
            else
            {
                HandleLeftMouseReleased();
            }
        }

        private void HandleLeftMousePressed(Vector2 mousePos)
        {
            _scene.SelectedPosePointIndex = -1;

            if (TrySelectPosePoint(mousePos)) return;

            TrySelectRotationBone(mousePos);
        }

        private bool TrySelectPosePoint(Vector2 mousePos)
        {
            if (_scene.SelectedStartPoseIndex != -1 && _scene.SelectedStartPoseIndex < PoseIndex.POSES.Count)
            {
                var startPose = PoseIndex.POSES[_scene.SelectedStartPoseIndex];
                for (int i = 0; i < startPose.IkTargetPositions.Count; i++)
                {
                    if (startPose.IkTargetPositions[i].DistanceTo(mousePos) < IkRiggingScene.SELECTION_RADIUS)
                    {
                        _scene.SelectedPosePointIndex = i;
                        _scene.IsStartPosePoint = true;
                        _scene.IsRotatingBone = false;
                        _scene.SetSliderValue(0);
                        return true;
                    }
                }
            }

            if (_scene.SelectedEndPoseIndex != -1 && _scene.SelectedEndPoseIndex < PoseIndex.POSES.Count)
            {
                var endPose = PoseIndex.POSES[_scene.SelectedEndPoseIndex];
                for (int i = 0; i < endPose.IkTargetPositions.Count; i++)
                {
                    if (endPose.IkTargetPositions[i].DistanceTo(mousePos) < IkRiggingScene.SELECTION_RADIUS)
                    {
                        _scene.SelectedPosePointIndex = i;
                        _scene.IsStartPosePoint = false;
                        _scene.IsRotatingBone = false;
                        _scene.SetSliderValue(1);
                        return true;
                    }
                }
            }
            return false;
        }

        private void TrySelectRotationBone(Vector2 mousePos)
        {
            bool found = false;
            Friflo.Engine.ECS.Entity hitBone = default;
            Vector2 hitPos = default;

            _scene.BonesQuery.ForEachEntity((ref BoneEcs bone, ref BoneGlobalTransform xform, Friflo.Engine.ECS.Entity e) =>
            {
                if (found) return;
                if (_scene.IkBones.Contains(e)) return;
                var jointPos = xform.GlobalPosition;
                if (jointPos.DistanceTo(mousePos) <= IkRiggingScene.SELECTION_RADIUS * 0.75f)
                {
                    found = true;
                    hitBone = e;
                    hitPos = jointPos;
                }
            });

            if (found)
            {
                _scene.SelectedRotationBone = hitBone;
                _scene.IsRotatingBone = true;
                ref var bone = ref _scene.SelectedRotationBone.GetComponent<BoneEcs>();
                _scene.BoneStartAngle = bone.Angle;
                _scene.GrabStartAngle = (mousePos - hitPos).Angle();
                _scene.QueueRedraw();
            }
        }

        private void HandleLeftMouseReleased()
        {
            if (_scene.SelectedPosePointIndex != -1)
            {
                var modifiedPose = _scene.IsStartPosePoint ? PoseIndex.POSES[_scene.SelectedStartPoseIndex] : PoseIndex.POSES[_scene.SelectedEndPoseIndex];
                modifiedPose.BoneAngles = _scene.AllBones.Select(b => _scene.IkBones.Contains(b) ? float.NaN : b.GetComponent<BoneEcs>().Angle).ToList();
                PoseIndex.Save();
            }

            _scene.SelectedPosePointIndex = -1;
            _scene.IsRotatingBone = false;
            _scene.QueueRedraw();
        }

        private void HandleMouseMotion(InputEventMouseMotion mouseMotionEvent)
        {
            var mousePos = _scene.GetGlobalMousePosition();
            if (_scene.SelectedPosePointIndex != -1)
            {
                HandleDragPosePoint(mousePos);
            }
            else if (_scene.IsRotatingBone)
            {
                HandleRotateBone(mousePos);
            }
        }

        private void HandleDragPosePoint(Vector2 mousePos)
        {
            if (_scene.IsStartPosePoint)
            {
                if (_scene.SelectedStartPoseIndex != -1 && _scene.SelectedStartPoseIndex < PoseIndex.POSES.Count)
                {
                    PoseIndex.POSES[_scene.SelectedStartPoseIndex].IkTargetPositions[_scene.SelectedPosePointIndex] = mousePos;
                }
            }
            else
            {
                if (_scene.SelectedEndPoseIndex != -1 && _scene.SelectedEndPoseIndex < PoseIndex.POSES.Count)
                {
                    PoseIndex.POSES[_scene.SelectedEndPoseIndex].IkTargetPositions[_scene.SelectedPosePointIndex] = mousePos;
                }
            }

            _scene.OnSliderValueChanged(_scene.GetSliderValue());
            _scene.QueueRedraw();
        }

        private void HandleRotateBone(Vector2 mousePos)
        {
            ref var bone = ref _scene.SelectedRotationBone.GetComponent<BoneEcs>();
            ref var xform = ref _scene.SelectedRotationBone.GetComponent<BoneGlobalTransform>();

            var jointPos = xform.GlobalPosition;
            var currentAngle = (mousePos - jointPos).Angle();
            var delta = Mathf.AngleDifference(currentAngle, _scene.GrabStartAngle);
            bone.Angle = _scene.BoneStartAngle + delta;

            _scene.QueueRedraw();
        }
    }
}
