// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.Audio;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class Root: SystemRoot
    {
        public Root(EntityStore entityStore, TrackHandler trackHandler)
        {
            AddStore(entityStore);

            Add(new NoteBaseSystem(trackHandler));
            Add(new NoteHitSystem(trackHandler));
            Add(new ColorTrackSystem(trackHandler));
            Add(new TempValueApplySystem());
        }
    }
}
