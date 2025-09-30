// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using ZLinq;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public static class KeyframeManager<T>
    {
        public static void AddFrame(ref T[] values, ref float[] frames, float time, T defaultValue)
        {
            frames = frames.AsValueEnumerable().Append(time).ToArray();
            values = values.AsValueEnumerable().Append(defaultValue).ToArray();
        }

        public static void RemoveFrame(ref T[] values, ref float[] frames, int index)
        {
            var valuesTemp = values.ToList();
            var framesTemp = frames.ToList();

            valuesTemp.RemoveAt(index);
            framesTemp.RemoveAt(index);

            values = valuesTemp.ToArray();
            frames = framesTemp.ToArray();
        }

        public static void SortKeyframes(ref T[] values, ref float[] frames)
        {
            var combinedKeyframes = frames.AsValueEnumerable().Zip(values,
                    (frameValue, colorValue) => new { Frame = frameValue, ColorVal = colorValue })
                .ToList();

            var sortedKeyframes = combinedKeyframes.AsValueEnumerable()
                .OrderBy(keyframe => keyframe.Frame)
                .ToList();

            frames = sortedKeyframes.AsValueEnumerable().Select(keyframe => keyframe.Frame).ToArray();
            values = sortedKeyframes.AsValueEnumerable().Select(keyframe => keyframe.ColorVal).ToArray();
        }

        public static void UpdateKeyframePosition(ref float[] frames, int index, float time)
        {
            frames[index] = time;
        }

        public static int SelectNearestFrameIndex(float[] frames, float time)
        {
            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < frames.Length; i++)
            {
                float distance = Math.Abs(frames[i] - time);
                if (!(distance < nearestDistance)) continue;
                nearestDistance = distance;
                nearestIndex = i;
            }

            return nearestDistance > 0.05f ? -1 : nearestIndex;
        }
    }
}
