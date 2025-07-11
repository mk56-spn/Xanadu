// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.DataStructure
{
    [GlobalClass]
    public partial class TrackInfo : Resource
    {
        public required string SongTitle;
        public required (double timingPoint, double bpm)[] TimingPoints { get; init; }
        public int Measures { get; set; }
        public required string Track { get; set; }
    }
}
