// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public static class ItemTransformExtensions
    {
        public static void AddKeyframe(this Entity entity, float time, Transform2D transform, bool isVisible = true)
        {
            if (!entity.TryGetComponent<ItemTransformTrackEcs>(out var track))
            {
                track = new ItemTransformTrackEcs { Keyframes = new ItemTransformKeyframe[] { } };
                entity.AddComponent(track);
            }

            var newKeyframe = new ItemTransformKeyframe(time, transform, isVisible);
            var keyframes = track.Keyframes;

            // Find insertion point to keep keyframes sorted by time
            int insertIndex = 0;
            for (; insertIndex < keyframes.Length; insertIndex++)
            {
                if (keyframes[insertIndex].Time > time)
                    break;
                if (Mathf.IsEqualApprox(keyframes[insertIndex].Time, time))
                {
                    // Replace existing keyframe at the same time
                    keyframes[insertIndex] = newKeyframe;
                    entity.AddComponent(new ItemTransformTrackEcs { Keyframes = keyframes });
                    return;
                }
            }

            // Insert new keyframe
            var newKeyframes = new ItemTransformKeyframe[keyframes.Length + 1];
            System.Array.Copy(keyframes, 0, newKeyframes, 0, insertIndex);
            newKeyframes[insertIndex] = newKeyframe;
            System.Array.Copy(keyframes, insertIndex, newKeyframes, insertIndex + 1, keyframes.Length - insertIndex);

            entity.AddComponent(new ItemTransformTrackEcs { Keyframes = newKeyframes });
        }

        public static void RemoveKeyframe(this Entity entity, float time)
        {
            if (!entity.TryGetComponent<ItemTransformTrackEcs>(out var track))
                return;

            var keyframes = track.Keyframes;
            int removeIndex = -1;

            for (int i = 0; i < keyframes.Length; i++)
            {
                if (Mathf.IsEqualApprox(keyframes[i].Time, time))
                {
                    removeIndex = i;
                    break;
                }
            }

            if (removeIndex == -1)
                return;

            var newKeyframes = new ItemTransformKeyframe[keyframes.Length - 1];
            System.Array.Copy(keyframes, 0, newKeyframes, 0, removeIndex);
            System.Array.Copy(keyframes, removeIndex + 1, newKeyframes, removeIndex, keyframes.Length - removeIndex - 1);

            entity.AddComponent(new ItemTransformTrackEcs { Keyframes = newKeyframes });
        }

    }
}
