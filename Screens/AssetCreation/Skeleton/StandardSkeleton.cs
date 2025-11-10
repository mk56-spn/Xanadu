// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using XanaduProject.IO;

namespace XanaduProject.Screens.AssetCreation.Skeleton
{
    [MemoryPackable]
    public partial class StandardPoseSkin
    {
        public string Name { get; }
        public string Description { get; }
        public string Author { get; }
        public List<string> BoneNames { get; }
        public Dictionary<string, Item> ItemAssignments { get; }

        [MemoryPackConstructor]
        public StandardPoseSkin(
            string name,
            string description,
            string author,
            List<string> boneNames,
            Dictionary<string, Item> itemAssignments)
        {
            Name = name;
            Description = description;
            Author = author;
            BoneNames = boneNames;
            ItemAssignments = itemAssignments;
        }

        public StandardPoseSkin(IEnumerable<string> boneNames) : this(
            "Standard Skeleton",
            "A standard pose for a mesh.",
            "mk56_spn",
            boneNames.ToList(),
            new Dictionary<string, Item>()
        )
        {
        }
    }
}
