// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Perceptions;

namespace XanaduProject.Screens
{
    [GlobalClass]
    public partial class Stage : WorldEnvironment
    {
        public readonly Core Core;

        public Stage()
        {
            var coreScene = ResourceLoader.Load<PackedScene>("res://Perceptions/Core.tscn");
            AddChild(Core = coreScene.Instantiate<Core>());
        }
    }
}
