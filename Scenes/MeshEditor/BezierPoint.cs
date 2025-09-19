// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using Godot;
using XanaduProject.Serialization;

namespace XanaduProject.Scenes.MeshEditor
{
    public struct BezierPoint(Vector2 position)
    {
        [JsonIgnore]
        public bool HandlesLocked { get; set; } // Changed from field to property
        [JsonConverter(typeof(Vector2Converter))]
        public Vector2 Position { get; set; } = position;
        [JsonConverter(typeof(Vector2Converter))]
        public Vector2 InHandle { get; set; } = new(-20, 0); // Default handle offset
        // Offset from Position
        [JsonConverter(typeof(Vector2Converter))]
        public Vector2 OutHandle { get; set; } = new(20, 0); // Default handle offset
        // Offset from Position
    }
}
