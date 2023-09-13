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
    public partial class Composer : StageHandler, IProvide<Camera2D>
    {
        public override partial void _Notification(int what);

        Camera2D IProvide<Camera2D>.Value() => Camera;

        public Composer() : base(new PanningCamera())
        {
            Provide();
            ChildEnteredTree += AddSelectionBody;
        }

        public static void AddSelectionBody(Node node)
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
