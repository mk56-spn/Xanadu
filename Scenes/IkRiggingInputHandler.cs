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
            _scene.selectedPosePointIndex = -1;

            if (TrySelectPosePoint(mousePos)) return;

            TrySelectRotationBone(mousePos);
        }

        private bool TrySelectPosePoint(Vector2 mousePos)
        {
            if (_scene.selectedStartPoseIndex != -1 && _scene.selectedStartPoseIndex < PoseIndex.POSES.Count)
            {
                var startPose = PoseIndex.POSES[_scene.selectedStartPoseIndex];
                for (int i = 0; i < startPose.IkTargetPositions.Count; i++)
                {
                    if (startPose.IkTargetPositions[i].DistanceTo(mousePos) < IkRiggingScene.SELECTION_RADIUS)
                    {
                        _scene.selectedPosePointIndex = i;
                        _scene.isStartPosePoint = true;
                        _scene.isRotatingBone = false;
                        _scene.SetSliderValue(0);
                        return true;
                    }
                }
            }

            if (_scene.selectedEndPoseIndex != -1 && _scene.selectedEndPoseIndex < PoseIndex.POSES.Count)
            {
                var endPose = PoseIndex.POSES[_scene.selectedEndPoseIndex];
                for (int i = 0; i < endPose.IkTargetPositions.Count; i++)
                {
                    if (endPose.IkTargetPositions[i].DistanceTo(mousePos) < IkRiggingScene.SELECTION_RADIUS)
                    {
                        _scene.selectedPosePointIndex = i;
                        _scene.isStartPosePoint = false;
                        _scene.isRotatingBone = false;
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

            _scene.bonesQuery.ForEachEntity((ref BoneEcs bone, ref BoneGlobalTransform xform, Friflo.Engine.ECS.Entity e) =>
            {
                if (found) return;
                if (_scene.ikBones.Contains(e)) return;
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
                _scene.selectedRotationBone = hitBone;
                _scene.isRotatingBone = true;
                ref var bone = ref _scene.selectedRotationBone.GetComponent<BoneEcs>();
                _scene.boneStartAngle = bone.Angle;
                _scene.grabStartAngle = (mousePos - hitPos).Angle();
                _scene.QueueRedraw();
            }
        }

        private void HandleLeftMouseReleased()
        {
            if (_scene.selectedPosePointIndex != -1)
            {
                var modifiedPose = _scene.isStartPosePoint ? PoseIndex.POSES[_scene.selectedStartPoseIndex] : PoseIndex.POSES[_scene.selectedEndPoseIndex];
                modifiedPose.BoneAngles = _scene.allBones.Select(b => _scene.ikBones.Contains(b) ? float.NaN : b.GetComponent<BoneEcs>().Angle).ToList();
                PoseIndex.Save();
            }

            _scene.selectedPosePointIndex = -1;
            _scene.isRotatingBone = false;
            _scene.QueueRedraw();
        }

        private void HandleMouseMotion(InputEventMouseMotion mouseMotionEvent)
        {
            var mousePos = _scene.GetGlobalMousePosition();
            if (_scene.selectedPosePointIndex != -1)
            {
                HandleDragPosePoint(mousePos);
            }
            else if (_scene.isRotatingBone)
            {
                HandleRotateBone(mousePos);
            }
        }

        private void HandleDragPosePoint(Vector2 mousePos)
        {
            if (_scene.isStartPosePoint)
            {
                if (_scene.selectedStartPoseIndex != -1 && _scene.selectedStartPoseIndex < PoseIndex.POSES.Count)
                {
                    PoseIndex.POSES[_scene.selectedStartPoseIndex].IkTargetPositions[_scene.selectedPosePointIndex] = mousePos;
                }
            }
            else
            {
                if (_scene.selectedEndPoseIndex != -1 && _scene.selectedEndPoseIndex < PoseIndex.POSES.Count)
                {
                    PoseIndex.POSES[_scene.selectedEndPoseIndex].IkTargetPositions[_scene.selectedPosePointIndex] = mousePos;
                }
            }

            _scene.OnSliderValueChanged(_scene.GetSliderValue());
            _scene.QueueRedraw();
        }

        private void HandleRotateBone(Vector2 mousePos)
        {
            ref var bone = ref _scene.selectedRotationBone.GetComponent<BoneEcs>();
            ref var xform = ref _scene.selectedRotationBone.GetComponent<BoneGlobalTransform>();

            var jointPos = xform.GlobalPosition;
            var currentAngle = (mousePos - jointPos).Angle();
            var delta = Mathf.AngleDifference(currentAngle, _scene.grabStartAngle);
            bone.Angle = _scene.boneStartAngle + delta;

            _scene.QueueRedraw();
        }
    }
}
