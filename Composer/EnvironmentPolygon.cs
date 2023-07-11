// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Composer
{
    [Tool]
    [GlobalClass]
    public partial class EnvironmentPolygon : Polygon2D
    {
        private StaticBody2D body = new StaticBody2D { CollisionLayer = 4 };
        private CollisionPolygon2D hitBox = new CollisionPolygon2D { OneWayCollision = true };

        public EnvironmentPolygon()
        {
            AddChild(body);
            body.AddChild(hitBox);

            Draw += () => hitBox.Polygon = Polygon;
        }
    }
}
