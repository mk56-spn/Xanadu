// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Presets;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.GD;

namespace XanaduProject.Tests
{
	/// <summary>
	/// Creates a randomised instance of a <see cref="SerializableStage"/>
	/// </summary>
	public class TestSerializableStage : SerializableStage
	{
		private int area = 10000;
		public TestSerializableStage (int noteCount, int textureElementCount, int textElementCount)
		{
			addTextures(textElementCount, textureElementCount);
		}
		public TestSerializableStage ()
		{
			addTextures(RandRange(0, 5000), RandRange(0, 10000));
		}

		private void addTextures(int textElementCount, int textureElementCount)
		{
			EntityStore = new EntityStore();

			for (int i = 0; i < 400; i++)
			{
				if (i % 5 == 0)
				{
					NoteType[] values =
					[
						NoteType.Up,
						NoteType.Left,
						NoteType.Right
					];

					 NoteType n = values[RandRange(0,2)];

					 PrefabEntity.Note(EntityStore.CreateEntity(),
						 new Vector2(RandRange(0, area), RandRange(0, area)),
						 0.3F * RandRange(0, 500), n, null! );

					EntityStore.CreateEntity(new ElementEcs
						{
							Colour = Colors.White,
							Transform = Transform2D.Identity with
							{
								Origin = new Vector2(RandRange(0, area / 10), RandRange(0, area / 10))
							},
						},
						new NoteEcs(0.3F * RandRange(0, 500), n));
				}


				EntityStore.CreateEntity(new ElementEcs {
						Transform = Transform2D.Identity with {Origin = new Vector2(RandRange(0,area / 3),RandRange(0,area / 3))},
						Colour = Colors.White,
					},
					new BlockEcs(),
					new RectEcs
					{
						Filled = RandRange(0,1) == 0,
						Extents = new Vector2(GD.RandRange(0,99), GD.RandRange(0,99))
					});

				Vector2[] points = new Vector2[RandRange(4,4)];

				for (int j = 0; j < points.Length; j++)
				{
					points[j] = new Vector2(GD.RandRange(-4,4),GD.RandRange(-4,4)) * 64;
				}

				points[^1] = points[0];

				EntityStore.CreateEntity(new ElementEcs {
						Transform = Transform2D.Identity with {Origin = new Vector2(RandRange(0,area),RandRange(0,area))},
						Colour = Colors.White,
					},
				   new PolygonEcs { Points = points, });
			}
		}

		private readonly float[] notes =
		[
			0.6f,
			1.2f,
			1.8f,
			2.4f,
			6f,
			9.3f,
			12f
	   ];
	}
}
