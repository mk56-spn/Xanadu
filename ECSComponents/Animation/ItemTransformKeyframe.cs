// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.Animation2
{
    public struct ItemTransformKeyframe
    {
        public float Time;
        public Transform2D Transform;
        public bool IsVisible;

        public ItemTransformKeyframe(float time, Transform2D transform, bool isVisible = true)
        {
            Time = time;
            Transform = transform;
            IsVisible = isVisible;
        }
    }

    public struct ItemTransformTrackEcs : IComponent
    {
        public ItemTransformKeyframe[] Keyframes;
    }
}
