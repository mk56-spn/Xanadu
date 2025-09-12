using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;

namespace XanaduProject.Scenes
{
    public class IkRiggingRenderer
    {
        private readonly IkRiggingScene scene;

        public IkRiggingRenderer(IkRiggingScene scene)
        {
            this.scene = scene;
        }

        public void Draw()
        {
            if (scene.SelectedStartPoseIndex != -1 && scene.SelectedEndPoseIndex != -1 &&
                scene.SelectedStartPoseIndex < PoseIndex.POSES.Count && scene.SelectedEndPoseIndex < PoseIndex.POSES.Count)
            {
                DrawPoseConnections();
            }

            DrawIkTargets();
            DrawRotationWidgets();
        }

        private void DrawPoseConnections()
        {
            var startPose = PoseIndex.POSES[scene.SelectedStartPoseIndex];
            var endPose = PoseIndex.POSES[scene.SelectedEndPoseIndex];

            for (int i = 0; i < startPose.IkTargetPositions.Count; i++)
            {
                if (i < endPose.IkTargetPositions.Count)
                {
                    scene.DrawLine(startPose.IkTargetPositions[i], endPose.IkTargetPositions[i], Colors.Gray, 1);
                }

                scene.DrawCircle(startPose.IkTargetPositions[i], IkRiggingScene.SELECTION_RADIUS, new Color(0, 1, 0, 0.5f));
            }

            for (int i = 0; i < endPose.IkTargetPositions.Count; i++)
            {
                scene.DrawCircle(endPose.IkTargetPositions[i], IkRiggingScene.SELECTION_RADIUS, new Color(0, 0, 1, 0.5f));
            }
        }

        private void DrawIkTargets()
        {
            scene.IkTargetsQuery.ForEachEntity((ref IkTargetComponent ikTarget, Friflo.Engine.ECS.Entity entity) =>
            {
                scene.DrawCircle(ikTarget.TargetPosition, 5, Colors.Red);
            });
        }

        private void DrawRotationWidgets()
        {
            scene.BonesQuery.ForEachEntity((ref BoneEcs bone, ref BoneGlobalTransform xform, Friflo.Engine.ECS.Entity e) =>
            {
                if (scene.IkBones.Contains(e)) return;

                var jointPos = xform.GlobalPosition;
                var ringColor = Colors.Yellow;
                var thickness = 2f;

                if (scene.IsRotatingBone && e.Equals(scene.SelectedRotationBone))
                {
                    ringColor = new Color(1f, 0.85f, 0.2f);
                    thickness = 3f;

                    var mouse = scene.GetGlobalMousePosition();
                    scene.DrawLine(jointPos, mouse, ringColor, 1.5f);
                }

                scene.DrawArc(jointPos, IkRiggingScene.SELECTION_RADIUS * 0.75f, 0, Mathf.Tau, 24, ringColor, thickness);
            });
        }
    }
}
