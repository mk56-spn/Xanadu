// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Stage.Masters.Rendering
{
    public static class InstanceShaders
    {
        private static readonly StringName note_position= "note_pos";
        private static readonly StringName hit_position = "hit_pos";

        public static void SetNoteTime(Rid rid, float time) =>
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, note_position, time);
        public static void SetHitTime(Rid rid, float time) =>
            RenderingServer.CanvasItemSetInstanceShaderParameter(rid, hit_position, time);
    }
}
