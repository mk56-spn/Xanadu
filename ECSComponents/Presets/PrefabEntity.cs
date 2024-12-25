// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.ECSComponents.Presets
{
	public static class PrefabEntity
	{
		public static void Polygon(Entity entity, Vector2 pos, Vector2[] points) =>
			entity.Add(createElement(pos), new PolygonEcs { Points = points });

		public static void Note(Entity entity, Vector2 pos, float timing, NoteType t, World2D world)
		{
			ElementEcs e;
			entity.Add(e = createElement(pos), createNote(timing,t), HitZoneEcs.Create(e, world));
		}

	   /* public static void HoldNote(Entity entity, Vector2 pos, float timing, NoteType t, float duration, Vector2 endPosition) =>
dn			entity.Add(createElement(pos), createNote(timing,t), new HoldEcs { Duration = duration, EndPosition = endPosition});*/

		public static void Rect(Entity entity, Vector2 pos, Vector2 extents) =>
			entity.Add(createElement(pos), createRect(extents));



		public static void Block(Entity entity, Vector2 pos, Vector2 extents) =>
			entity.Add(createElement(pos), new BlockEcs(), createRect(extents));

		private static RectEcs createRect(Vector2 extents) => new() { Extents = extents, LineWidth = 6};
		private static NoteEcs createNote(float timing, NoteType type) => new(timing,type);
		private static ElementEcs createElement(Vector2 pos) => new()
		{
				Transform = Transform2D.Identity with { Origin = pos },
				Colour = Colors.White,
			};
	}
}
