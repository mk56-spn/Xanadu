// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XanaduProject.DataStructure
{
	public struct TrackInfo
	{
		public required string SongTitle { get; set; }
		public required TimingPoint[] TimingPoints { get; set; }
        public int Measures { get; set; }

		[JsonIgnore] public string Track { get; set; } = null!;

        public TrackInfo()
        {
        }
	}

    public struct TimingPoint
    {
        public double Value { get; set; }
        public double Bpm { get; set; }

        [JsonConstructor]
        public TimingPoint(double value, double bpm)
        {
            Value = value;
            Bpm = bpm;
        }
    }
}
