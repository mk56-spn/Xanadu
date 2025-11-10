// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using XanaduProject.Factories;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.Components
{
    public readonly struct DepthEcs(int depth) : IComponent
    {
        public readonly int Depth = depth;
    }
    public readonly struct AnimationTarget(Entity entity) : ILinkComponent
    {
        public readonly Entity Entity = entity;
        public Entity GetIndexedValue() => Entity;
    }

    public readonly struct IkControlled : ITag;

    public struct AnimationInfo() : IComponent
    {
        public float Duration = 1;
        public float AnimationPos = 0;

        public AnimationMode Mode = AnimationMode.Loop;

        public bool LerpEndToBeginning;
        public bool LerpBeginningFromPose;
        public bool DefaultToPose;

        [Ignore]
        public AnimationState AnimationActive = AnimationState.Disabled;

        [Ignore] public bool Playing = false;
    }

    public enum AnimationMode
    {
        Loop,
        Once,
        PingPong
    }

    public enum AnimationState
    {
        Playing,
        Active,
        Disabled
    }

    public static class AnimationModeHelpers
    {
        public static string ToDisplayString(this AnimationMode mode) => mode switch
        {
            AnimationMode.Loop => "↻",
            AnimationMode.Once => "→",
            AnimationMode.PingPong => "⇄",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}
