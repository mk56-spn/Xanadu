// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using Friflo.Engine.ECS;
using XanaduProject.Factories;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating.Components
{
    public readonly struct DepthEcs(int depth)
    {
        public readonly int Depth = depth;
    }
    public readonly struct AnimationTarget(Entity entity) : ILinkComponent
    {
        public readonly Entity Entity = entity;
        public Entity GetIndexedValue() => Entity;
    }

    public readonly struct IkControlled : ITag;

    public struct AnimationInfo(bool looping = false) : IComponent
    {
        public bool Looping = looping;
        public float Duration = 1;
        public float AnimationPos = 0;
    }

    public struct CanvasEcs() : IComponent
    {
        public RenderRid Canvas = RenderRid.Create();
    }
}
