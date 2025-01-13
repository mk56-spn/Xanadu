// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.Animation
{
    public struct ColorTrack : IComponent
    {
        public ColorKeyFrame[] KeyFrames;
        public readonly Color LerpedFrameValue(float timingPoint)
        {
                if (KeyFrames.Length == 0)
                    return default!;

                if (timingPoint <= KeyFrames[0].Time)
                    return KeyFrames[0].Color;
                if (timingPoint >= KeyFrames[^1].Time)
                    return KeyFrames[^1].Color;

                ColorKeyFrame prevKeyframe = KeyFrames[0];
                ColorKeyFrame nextKeyframe = KeyFrames[^1];

                for (int i = 1; i < KeyFrames.Length; i++)
                    if (KeyFrames[i].Time > timingPoint)
                    {
                        nextKeyframe = KeyFrames[i];
                        prevKeyframe = KeyFrames[i - 1];
                        break;
                    }

                float t = (timingPoint - prevKeyframe.Time) / (nextKeyframe.Time - prevKeyframe.Time);

                return prevKeyframe.Color.Lerp(nextKeyframe.Color, t);
        }

        public static Color AverageColors(Color[] colors)
            {
                float totalA = 0, totalR = 0, totalG = 0, totalB = 0;
                int colorCount = colors.Length;

                if (colorCount == 0)
                {
                    return Colors.White; // Default to transparent if no colors are provided
                }

                foreach (Color color in colors)
                {
                    totalA += color.A;
                    totalR += color.R;
                    totalG += color.G;
                    totalB += color.B;
                }

                float avgA = totalA / colorCount;
                float avgR = totalR / colorCount;
                float avgG = totalG / colorCount;
                float avgB = totalB / colorCount;

                return new Color(avgR, avgG, avgB, avgA);
            }
        }
}
