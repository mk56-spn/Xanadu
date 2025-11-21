// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.GameDependencies;
using XanaduProject.Utils;

namespace XanaduProject.ECSComponents.EntitySystem.ColourChannels
{
    public class TrackLerpColours : QuerySystem<FloatArrayEcs, ColorArrayEcs, ActiveColourEcs, IndexEcs>
    {
        protected override void OnUpdate()
        {
            Query.Each(new TrackColorLerp());
        }

        private readonly struct TrackColorLerp() : IEach<FloatArrayEcs, ColorArrayEcs, ActiveColourEcs, IndexEcs>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(ref FloatArrayEcs floats, ref ColorArrayEcs colors, ref ActiveColourEcs active, ref IndexEcs index)
            {
                active.Color = UtilsGeneral.LerpedFrameValue<Color>((float)DiProvider.Get<IClock>().PlaybackTimeSec,
                    floats.Points, colors.Points, []);
            }
        }
    }
}
