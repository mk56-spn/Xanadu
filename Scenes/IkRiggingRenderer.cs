using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Scenes
{
    public class IkRiggingRenderer
    {
        private readonly IkRiggingScene _scene;

        public IkRiggingRenderer(IkRiggingScene scene)
        {
            _scene = scene;
        }

        public void Draw()
        {
            if (_scene.selectedStartPoseIndex != -1 && _scene.selectedEndPoseIndex != -1 &&
                _scene.selectedStartPoseIndex < PoseIndex.POSES.Count && _scene.selectedEndPoseIndex < PoseIndex.POSES.Count)
            {
                DrawPoseConnections();
            }

            DrawIkTargets();
            DrawRotationWidgets();
        }

        private void DrawPoseConnections()
        {
            var startPose = PoseIndex.POSES[_scene.selectedStartPoseIndex];
            var endPose = PoseIndex.POSES[_scene.selectedEndPoseIndex];

            for (int i = 0; i < startPose.IkTargetPositions.Count; i++)
            {
                if (i < endPose.IkTargetPositions.Count)
                {
                    _scene.DrawLine(startPose.IkTargetPositions[i], endPose.IkTargetPositions[i], Colors.Gray, 1);
                }

                _scene.DrawCircle(startPose.IkTargetPositions[i], IkRiggingScene.SELECTION_RADIUS, new Color(0, 1, 0, 0.5f));
            }

            for (int i = 0; i < endPose.IkTargetPositions.Count; i++)
            {
                _scene.DrawCircle(endPose.IkTargetPositions[i], IkRiggingScene.SELECTION_RADIUS, new Color(0, 0, 1, 0.5f));
            }
        }

        private void DrawIkTargets()
        {
            _scene.ikTargetsQuery.ForEachEntity((ref IkTargetComponent ikTarget, Friflo.Engine.ECS.Entity entity) =>
            {
                _scene.DrawCircle(ikTarget.TargetPosition, 5, Colors.Red);
            });
        }

        private void DrawRotationWidgets()
        {
            _scene.bonesQuery.ForEachEntity((ref BoneEcs bone, ref BoneGlobalTransform xform, Friflo.Engine.ECS.Entity e) =>
            {
                if (_scene.ikBones.Contains(e)) return;

                var jointPos = xform.GlobalPosition;
                var ringColor = Colors.Yellow;
                var thickness = 2f;

                if (_scene.isRotatingBone && e.Equals(_scene.selectedRotationBone))
                {
                    ringColor = new Color(1f, 0.85f, 0.2f);
                    thickness = 3f;

                    var mouse = _scene.GetGlobalMousePosition();
                    _scene.DrawLine(jointPos, mouse, ringColor, 1.5f);
                }

                _scene.DrawArc(jointPos, IkRiggingScene.SELECTION_RADIUS * 0.75f, 0, Mathf.Tau, 24, ringColor, thickness);
            });
        }
    }
}
