// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using XanaduProject.IO;

namespace XanaduProject.Scenes.ItemEditor
{
    public class StandardPoseSkin(
        string name,
        string description,
        string author,
        List<string> boneNames,
        Dictionary<string, Item> itemAssignments
    )
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string Author { get; } = author;
        public List<string> BoneNames { get; } = boneNames;
        public Dictionary<string, Item> ItemAssignments { get; } = itemAssignments;

        // This constructor is for creating a new, empty skin with just bone names.
        public StandardPoseSkin(IEnumerable<string> boneNames) : this(
            "Standard Pose",
            "A standard pose for a mesh.",
            "mk56_spn",
            boneNames.ToList(),
            new Dictionary<string, Item>()
        )
        {
        }
    }
}
