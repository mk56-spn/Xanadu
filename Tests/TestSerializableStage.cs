// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.GD;

namespace XanaduProject.Tests
{
	/// <summary>
	/// Creates a randomised instance of a <see cref="SerializableStage"/>
	/// </summary>
	public class TestSerializableStage : SerializableStage
	{
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

			for (int i = 0; i < 100; i++)
			{
				EntityStore.CreateEntity(new ElementEcs {
					Colour = Colors.White,
					Transform =  Transform2D.Identity with {Origin = new Vector2(RandRange(0,1000),RandRange(0,1000))},
				},
					new NoteEcs { TimingPoint = 0.3F * RandRange(0, 500) },
					new AreaEcs());
				EntityStore.CreateEntity(new ElementEcs {
					Transform = Transform2D.Identity with {Origin = new Vector2(RandRange(0,1000),RandRange(0,1000))},
					Colour = Colors.White,
				},
					new RectEcs { Extents = new Vector2(GD.RandRange(0,99), GD.RandRange(0,99))});


				Vector2[] points = new Vector2[GD.RandRange(3,3)];
				Color[] colors = new Color[points.Length];

				for (int j = 0; j < points.Length; j++)
				{
					colors[j] = new Color(GD.Randf(),GD.Randf(),GD.Randf());
					points[j] = new Vector2(GD.RandRange(0,1000),GD.RandRange(0,1000));
				}

				colors[^1] = colors[0];
				points[^1] = points[0];

				EntityStore.CreateEntity(new ElementEcs {
						Transform = Transform2D.Identity with {Origin = new Vector2(RandRange(0,1000),RandRange(0,1000))},
						Colour = Colors.White,
					},
				   new PolygonEcs()
					{
						Points = points,
						Colors = colors
					}
					);
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
