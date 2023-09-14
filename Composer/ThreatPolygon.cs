// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Composer.Selectables;

namespace XanaduProject.Composer
{
    [Tool]
    [GlobalClass]
    public partial class ThreatPolygon : XanaduPolygon, IComposable
    {
        public Selectable Selectable  => new SelectablePolygon(this);

        private Area2D body = new Area2D();
        private CollisionPolygon2D hitBox = new CollisionPolygon2D();

        public ThreatPolygon()
        {
            AddChild(body);
            body.AddChild(hitBox);

            body.CollisionLayer = 8;

            Draw += () => hitBox.Polygon = Polygon;
        }
    }
}
