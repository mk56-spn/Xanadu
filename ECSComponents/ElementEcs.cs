// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Json.Fliox;
using Godot;
using XanaduProject.Composer;
using static Godot.RenderingServer;
using IComponent = Friflo.Engine.ECS.IComponent;

namespace XanaduProject.ECSComponents
{
	public struct ElementEcs() : IComponent
	{
		public Transform2D Transform = Transform2D.Identity;

		[Composer("Colour")] public Color Colour = Colors.Olive;

		[Composer("Index")]
		// ReSharper disable once MemberCanBePrivate.Global
		public int Index = 0;

		[Ignore] public Rid Canvas;

		[Ignore] public Vector2 Size = new(32, 32);

		public void UpdateCanvas(Color colour)
		{
			CanvasItemSetModulate(Canvas, colour);
		}

		public void SetTransform(Transform2D transform2D)
		{
			Transform = transform2D;
			CanvasItemSetTransform(Canvas, Transform);
		}

		public static Color ComposerColour = Colors.Red;


		public void SetDepth(int value)
		{
			GD.Print("calledS");
			Index = value;
			CanvasItemSetZIndex(Canvas, value);
		}

		public void SetRotation(float rotation)
		{
			Transform = Transform.RotatedLocal(rotation - Transform.Rotation);
			CanvasItemSetTransform(Canvas, Transform);
		}

		/// <summary>
		/// Sets non permanent scale
		/// </summary>
		/// <param name="value"></param>
		public void UpdateScale(Vector2 value)
		{
			CanvasItemSetTransform(Canvas, Transform.ScaledLocal(value));
		}

		public void CanvasCreate(Rid baseCanvas)
		{
			Canvas = CanvasItemCreate();
			CanvasItemSetParent(Canvas, baseCanvas);
		}
	}
}
