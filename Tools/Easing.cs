// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace XanaduProject.Tools
{
    public static class EasingFunctions
    {
        private static float linear(float t)
        {
            return t;
        }

        private static float inQuad(float t)
        {
            return t * t;
        }

        private static float outQuad(float t)
        {
            return 1 - inQuad(1 - t);
        }

        private static float inOutQuad(float t)
        {
            return t < 0.5f ? inQuad(t * 2) / 2 : 1 - inQuad((1 - t) * 2) / 2;
        }

        private static float inCubic(float t)
        {
            return t * t * t;
        }

        private static float outCubic(float t)
        {
            return 1 - inCubic(1 - t);
        }

        private static float inOutCubic(float t)
        {
            return t < 0.5f ? inCubic(t * 2) / 2 : 1 - inCubic((1 - t) * 2) / 2;
        }

        private static float inQuart(float t)
        {
            return t * t * t * t;
        }

        private static float outQuart(float t)
        {
            return 1 - inQuart(1 - t);
        }

        private static float inOutQuart(float t)
        {
            return t < 0.5f ? inQuart(t * 2) / 2 : 1 - inQuart((1 - t) * 2) / 2;
        }

        private static float inQuint(float t)
        {
            return t * t * t * t * t;
        }

        private static float outQuint(float t)
        {
            return 1 - inQuint(1 - t);
        }

        private static float inOutQuint(float t)
        {
            return t < 0.5f ? inQuint(t * 2) / 2 : 1 - inQuint((1 - t) * 2) / 2;
        }

        private static float inSine(float t)
        {
            return 1 - (float)Math.Cos(t * Math.PI / 2);
        }

        private static float outSine(float t)
        {
            return (float)Math.Sin(t * Math.PI / 2);
        }

        private static float inOutSine(float t)
        {
            return (float)(Math.Cos(t * Math.PI) - 1) / -2;
        }

        private static float inExpo(float t)
        {
            return (float)Math.Pow(2, 10 * (t - 1));
        }

        private static float outExpo(float t)
        {
            return 1 - inExpo(1 - t);
        }

        private static float inOutExpo(float t)
        {
            return t < 0.5f ? inExpo(t * 2) / 2 : 1 - inExpo((1 - t) * 2) / 2;
        }

        private static float inCirc(float t)
        {
            return -((float)Math.Sqrt(1 - t * t) - 1);
        }

        private static float outCirc(float t)
        {
            return 1 - inCirc(1 - t);
        }

        private static float inOutCirc(float t)
        {
            return t < 0.5f ? inCirc(t * 2) / 2 : 1 - inCirc((1 - t) * 2) / 2;
        }

        private static float inElastic(float t)
        {
            return 1 - outElastic(1 - t);
        }

        private static float outElastic(float t)
        {
            return (float)Math.Pow(2, -10 * t) * (float)Math.Sin((t - 0.3f / 4) * (2 * Math.PI) / 0.3f) + 1;
        }

        public static float InOutElastic(float t)
        {
            return t < 0.5f ? inElastic(t * 2) / 2 : 1 - inElastic((1 - t) * 2) / 2;
        }

        public static float InBack(float t)
        {
            return t * t * ((1.70158f + 1) * t - 1.70158f);
        }

        public static float OutBack(float t)
        {
            return 1 - InBack(1 - t);
        }

        public static float InOutBack(float t)
        {
            return t < 0.5f ? InBack(t * 2) / 2 : 1 - InBack((1 - t) * 2) / 2;
        }

        private static float inBounce(float t)
        {
            return 1 - outBounce(1 - t);
        }

        private static float outBounce(float t)
        {
            return t < 1f / 2.75f ? 7.5625f * t * t
                : t < 2f / 2.75f ? 7.5625f * (t - 1.5f / 2.75f) * (t - 1.5f / 2.75f) + 0.75f
                : t < 2.5f / 2.75f ? 7.5625f * (t - 2.25f / 2.75f) * (t - 2.25f / 2.75f) + 0.9375f
                : 7.5625f * (t - 2.625f / 2.75f) * (t - 2.625f / 2.75f) + 0.984375f;
        }

        private static float inOutBounce(float t)
        {
            return t < 0.5f ? inBounce(t * 2) / 2 : 1 - inBounce((1 - t) * 2) / 2;
        }

        private static readonly Dictionary<EasingType, Func<float, float>> easing_function_map = new()
        {
            { EasingType.Linear, linear },
            { EasingType.InQuad, inQuad },
            { EasingType.OutQuad, outQuad },
            { EasingType.InOutQuad, inOutQuad },
            { EasingType.InCubic, inCubic },
            { EasingType.OutCubic, outCubic },
            { EasingType.InOutCubic, inOutCubic },
            { EasingType.InQuart, inQuart },
            { EasingType.OutQuart, outQuart },
            { EasingType.InOutQuart, inOutQuart },
            { EasingType.InQuint, inQuint },
            { EasingType.OutQuint, outQuint },
            { EasingType.InOutQuint, inOutQuint },
            { EasingType.InSine, inSine },
            { EasingType.OutSine, outSine },
            { EasingType.InOutSine, inOutSine },
            { EasingType.InExpo, inExpo },
            { EasingType.OutExpo, outExpo },
            { EasingType.InOutExpo, inOutExpo },
            { EasingType.InCirc, inCirc },
            { EasingType.OutCirc, outCirc },
            { EasingType.InOutCirc, inOutCirc },
            { EasingType.InElastic, inElastic },
            { EasingType.OutElastic, outElastic },
            { EasingType.InOutElastic, InOutElastic },
            { EasingType.InBack, InBack },
            { EasingType.OutBack, OutBack },
            { EasingType.InOutBack, InOutBack },
            { EasingType.InBounce, inBounce },
            { EasingType.OutBounce, outBounce },
            { EasingType.InOutBounce, inOutBounce }
        };


        public static float GetEasing(EasingType easingType, float progress)
        {
            return easing_function_map[easingType](progress);
        }
    }

    public enum EasingType
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InSine,
        OutSine,
        InOutSine,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce
    }
}
