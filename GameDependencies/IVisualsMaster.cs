// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.GameDependencies
{
    public interface IVisualsMaster
    {
        CanvasLayer GameplayerLayer { get; }
        Rid GameplayerLayerRid { get; }
        Vector2 CameraPosition { get; set; }
    }
}
