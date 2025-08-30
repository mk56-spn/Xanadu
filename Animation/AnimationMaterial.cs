// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Audio;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Tools;

namespace XanaduProject.Animation
{
    public static partial class AnimationMaterial
    {
        public static readonly ShaderMaterial MATERIAL = new();
        private static readonly StringName animation_start_time = "animation_start_time";
        private static readonly StringName animation_duration = "animation_duration";
        private static readonly StringName easing_index = "easing_index";
        private static readonly StringName translation = "translation";
        private static readonly StringName rotation_degrees = "rotation_degrees";
        private static readonly StringName scaled = "scaled";
        private static readonly StringName center = "center";

        static AnimationMaterial()
        {
            MATERIAL.Shader = GD.Load<Shader>("uid://2qotdbl8i5q2");
        }

        private static IClock clock => DiProvider.Get<IClock>();

        public static void SetAnimationStartCurrent(RenderRid rid)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, animation_start_time, clock.PlaybackTimeSec);
        }

        public static void SetAnimationStartTime(RenderRid rid, float startTime)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, animation_start_time, startTime);
        }

        public static void SetAnimationDuration(RenderRid rid, float duration)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, animation_duration, duration);
        }

        public static void SetEasingIndex(RenderRid rid, EasingType easing)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, easing_index, (int)easing);
        }

        public static void SetTranslation(RenderRid rid, Vector2 translation)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, AnimationMaterial.translation, translation);
        }

        public static void SetRotationDegrees(RenderRid rid, float rotationDegrees)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, rotation_degrees, rotationDegrees);
        }

        public static void SetScaled(RenderRid rid, Vector2 scaled)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, AnimationMaterial.scaled, scaled);
        }

        public static void SetCenter(RenderRid rid, Vector2 center)
        {
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, AnimationMaterial.center, center);
        }
    }
}
