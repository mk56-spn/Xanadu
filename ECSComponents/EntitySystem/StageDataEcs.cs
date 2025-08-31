// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Friflo.Engine.ECS;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public struct StageDataEcs : IComponent
    {
        public required int SongIndex;

        public required string StageName;

        public required int Difficulty;

        public required string[] CreatorList;
    }
}
