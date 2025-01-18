// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Animation;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class ColorTrackSystem(TrackHandler trackHandler) : QuerySystem<ColorTrack>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity((ref ColorTrack component1, Entity entity) =>
            {
                var c = component1.LerpedFrameValue((float)trackHandler.TrackPosition);
                foreach (var cEntityLink in entity.GetIncomingLinks<ColorRelation>())
                {
                    var v = cEntityLink.Entity.GetComponent<ElementEcs>();
                    v.UpdateCanvas(c * v.Colour);
                }
            });
        }
    }
}
