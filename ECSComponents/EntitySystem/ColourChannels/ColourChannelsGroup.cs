// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.Animation;

namespace XanaduProject.ECSComponents.EntitySystem.ColourChannels
{
    public class ColourChannelsGroup : SystemGroup
    {
        public ColourChannelsGroup(string name) : base(name)
        {
            Add(new ColourTrackManager());
            Add(new TrackLerpColours());
            Add(new UpdateGpuTextureSystem());
            Add(new ChannelUpdateSystem());

        }
    }
}
