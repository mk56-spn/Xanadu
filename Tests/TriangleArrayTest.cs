// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Refresh_systems;

namespace XanaduProject.Tests
{
	[Tool]
	public partial class TriangleArrayTest : Node2D
	{
		private float width = 20.0f;
		private Color color = Colors.CornflowerBlue;

		public override void _Ready()
		{
			var panel = new PanelContainer
			{
			};

			panel.AddChild(tex);
			AddChild(panel);
			panel.AddChild(fps);
		}


		private Label tex = new()
		{
			ZIndex = 20,
			Modulate = Colors.Green
		};

		private Label fps = new()
		{
			LabelSettings = new LabelSettings
			{
				FontSize = 30
			},
			ZIndex = 20,
			Modulate = Colors.Red,
			Position = new Vector2(0, 300)
		};

		public override void _Process(double delta)
		{
			fps.Text = "\n \n \n \n  FPS: " + Engine.GetFramesPerSecond().ToString(CultureInfo.InvariantCulture);
		}


		public override void _Draw()
		{
			updateProceduralLine();
			RenderingServer.CanvasItemSetCustomRect(GetCanvasItem(), true,
				new Rect2(new Vector2(-10000, -10000), new Vector2(20000, 20000)));
		}


		private ArrayMesh mesh = null!;

		// In your C# script that controls the line:
		private void updateProceduralLine()
		{
			var material = (ShaderMaterial)Material;
			if (material == null) return;


			// Each segment is broken into `subdivisions` quads, and each quad needs 6 vertices.
			int vertexCount = 9_000;

			// A zero or negative vertex count will cause a crash, so guard against it.
			if (vertexCount <= 0)
			{
				RenderingServer.CanvasItemClear(GetCanvasItem());
				return;
			}


			// Clear previous drawing
			RenderingServer.CanvasItemClear(GetCanvasItem());

			// Create a new mesh surface
			var surfaceArray = new Godot.Collections.Array();
			surfaceArray.Resize((int)Mesh.ArrayType.Max);

			// We only need to provide an empty vertex array of the correct size.
			// The shader will calculate the actual positions using VERTEX_ID.
			var verts = new Vector2[vertexCount];
			surfaceArray[(int)Mesh.ArrayType.Vertex] = verts;

			// Add the surface to a new mesh
			mesh = new ArrayMesh();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

			// Draw the mesh using the shader
			RenderingServer.CanvasItemAddMesh(GetCanvasItem(), mesh.GetRid());
		}
	}
}
