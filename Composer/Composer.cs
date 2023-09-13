// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Composer.Notes;
using XanaduProject.Screens;
using XanaduProject.Composer.Selectables;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Provider))]
    public partial class Composer : StageHandler, IProvide<PanningCamera>
    {
        public override partial void _Notification(int what);

        PanningCamera IProvide<PanningCamera>.Value() => camera;
        private PanningCamera camera = new PanningCamera();

        public override void _Ready()
        {
            base._Ready();

            AddChild(camera);
            Provide();

            // Embeds a selectable shape into the node for use in composer editing.
            Stage.GetChildren().OfType<Node2D>().ToList().ForEach(AddSelectionBody);
        }

        public static void AddSelectionBody(Node2D node)
        {
            switch (node)
            {
                case Note:
                    node.AddChild(new SelectableNote());
                    return;
                case Polygon2D polygon:
                    node.AddChild(new SelectablePolygon(polygon));
                    return;
                case NoteLink noteLink:
                    noteLink.Notes.ToList().ForEach(n => n.AddChild(new SelectableNote()));
                    return;
            }
        }
    }
}
