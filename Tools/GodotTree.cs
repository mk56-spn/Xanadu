// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Tools
{
    public static class GodotTree
    {
        public static SceneTree Tree { get; private set; } = new();

        public static void Setup(Node node)
        {
            Tree = node.GetTree();
        }
    }
}
