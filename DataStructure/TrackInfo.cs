// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.DataStructure
{
    [GlobalClass]
    public partial class TrackInfo : Resource
    {
        [Export]
        public string SongTitle = null!;
        [Export]
        public double[] TimingPoints { get; set; } = null!;
        [Export]
        public double Bpm { get; set; }
        [Export]
        public int Measures { get; set; }
        [Export(PropertyHint.ResourceType)]
        public AudioStream Track { get; set; } = null!;
    }
}
