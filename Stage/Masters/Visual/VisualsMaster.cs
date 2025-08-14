// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Visual
{
    public partial class VisualsMaster : Node2D, IVisualsMaster
    {
        public CanvasLayer GameplayerLayer { get; } = new() { Layer = -1, FollowViewportEnabled = true };
        public Rid GameplayerLayerRid { get; }

        public Vector2 CameraPosition { get; set; }

        public VisualsMaster()
        {
            AddChild(GameplayerLayer);
            GameplayerLayerRid = GameplayerLayer.GetCanvas();
        }
    }
}
