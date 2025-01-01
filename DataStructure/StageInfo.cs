// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.DataStructure
{
    [GlobalClass]
    public partial class StageInfo : Resource
    {
        [Export]
        public int Difficulty { get; set; }
        [Export]
        public string Title { get; set; } = null!;
        [Export]
        public TrackInfo TrackInfo { get; set; } = null!;
        [Export]
        public string[] Designers { get; set; } = null!;
        [Export]
        public string Description { get; set; } = null!;
        [Export]
        private PackedScene stage { get; set; } = null!;

        [Export]
        private bool rLine { get; set; }
        [Export]
        private bool bLine { get; set; }
        [Export]
        private bool yLine { get; set; }

        // TODO: This is sketch af but it works for now i guess
    }
}
