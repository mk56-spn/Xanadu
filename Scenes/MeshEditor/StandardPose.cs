// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using XanaduProject.IO;

namespace XanaduProject.Scenes.MeshEditor
{
    public record StandardPoseSkin
    {
        public string Name { get; init; } = "Standard Pose";
        public string Description { get; init; } = "A standard pose for a mesh.";
        public string Author { get; init; } = "mk56_spn";
        public List<Item> Bones { get; init; }

        public StandardPoseSkin(IEnumerable<string> boneNames)
        {
            Bones = boneNames.Select(s => new Item { Name = s }).ToList();
        }
    }
}
