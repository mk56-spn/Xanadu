// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using JetBrains.Annotations;
using XanaduProject.Audio;
using XanaduProject.GameDependencies;
using static Godot.RenderingServer;

namespace XanaduProject.Factories
{
	public static class RenderingFactory
	{
		private static IClock clock => DiProvider.Get<IClock>();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AsRenderRid(this in Rid rid) => new(rid);

        #region Text

        private static readonly Font fallback_font = ThemeDB.FallbackFont;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderRid AddChar(this in RenderRid r, Vector2 position, char character, int fontSize, Color? color = null, Font? font = null)
        {
            (font ?? fallback_font).DrawChar(r.Rid, position, character, fontSize, color ?? Colors.White);
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderRid AddString(this in RenderRid r, Vector2 position, string text, int fontSize, Color? color = null, Font? font = null)
        {
            var actualFont = font ?? fallback_font;
            // Calculate string size to center it properly
            Vector2 stringSize = actualFont.GetStringSize(text, fontSize: fontSize);
            // Adjust position to center the text (offset by half the width)
            Vector2 centeredPosition = new Vector2(position.X - stringSize.X / 2, position.Y);

            actualFont.DrawString(r.Rid, centeredPosition, text, HorizontalAlignment.Left, -1, fontSize, color ?? Colors.White);
            return r;
        }


        #endregion

		#region Setters

		/// <summary>
		/// Sets a limited lifetime for a render item and frees its resource upon expiration.
		/// The removal time should not be depended upon for accuracy.
		/// </summary>
		/// <param name="rid">The render item whose lifetime is being managed.</param>
		/// <param name="time">The duration in seconds before the render item is freed.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async void SetLifetime(this RenderRid rid, float time)
		{
			double timeAtHit = clock.PlaybackTimeSec;

			while (clock.PlaybackTimeSec <= timeAtHit + time)
				await Task.Delay(100);

			FreeRid(rid.Rid);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid SetZIndex(this in RenderRid r, int z)
		{
			CanvasItemSetZIndex(r.Rid, z);
			return r;
		}

		/// <summary>
		/// Sets the global transformation of a render item using the provided Transform2D.
		/// </summary>
		/// <param name="r"></param>
		/// <param name="transform">The <see cref="Transform2D"/> value to be applied to the render item.</param>
		/// <returns>The modified <see cref="RenderRid"/> instance after applying the transformation.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid SetTransform(this in RenderRid r, in Transform2D transform)
		{
			CanvasItemSetTransform(r.Rid, transform);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddSetTransform(this in RenderRid r, in Transform2D transform)
		{
			CanvasItemAddSetTransform(r.Rid, transform);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid SetParent(this in RenderRid r, in Rid parent)
		{
			CanvasItemSetParent(r.Rid, parent);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid SetMaterial(this in RenderRid r, in Rid material)
		{
			CanvasItemSetMaterial(r.Rid, material);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid SetModulate(this in RenderRid r, in Color color)
		{
			CanvasItemSetModulate(r.Rid, color);
			return r;
		}

		#endregion

		#region ShapeAdders

		/// <summary>
		/// Adds a rectangular shape to the render item using the specified rectangle dimensions and optional colour.
		/// </summary>
		/// <param name="r">The <see cref="RenderRid"/> instance to which the rectangle will be added.</param>
		/// <param name="rect">The <see cref="Rect2"/> specifying the dimensions and position of the rectangle.</param>
		/// <param name="color">An optional <see cref="Color"/> to apply to the rectangle. Defaults to white if not specified.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddRect(this in RenderRid r, in Rect2 rect, Color? color = null)
		{
			CanvasItemAddRect(r.Rid, rect, color ?? Colors.White);
			return r;
		}

		[UsedImplicitly] private static GradientTexture2D gradientTexture2D = new()
		{
			Gradient = new Gradient(),
			Height = 30,
			Width = 30
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddParticles(this in RenderRid r, ParticlesRid particles)
		{
			CanvasItemAddParticles(r, particles, GetWhiteTexture());
            return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddParticles(this in RenderRid r, ParticlesRid particles, Rid texture)
		{
			CanvasItemAddParticles(r, particles, texture);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddRect(this in RenderRid r, Vector2 size, Color? color = null)
		{
			CanvasItemAddRect(r.Rid, new Rect2(-size / 2, size), color ?? Colors.White);
			return r;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddMultiline(this in RenderRid r, Vector2[] points, Color[]? color = null)
		{
			CanvasItemAddMultiline(r.Rid, points, color ?? [Colors.White]);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddMesh(this in RenderRid r, Rid mesh, Transform2D transform = default,
			Color? modulate = null, Rid texture = default, Rid normalMap = default)
		{
			CanvasItemAddMesh(r, mesh, transform, modulate ?? Colors.White, texture);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddCircle(this in RenderRid r,float radius, Vector2? position = null, Color? color = null)
		{
			CanvasItemAddCircle(r.Rid, position?? Vector2.Zero, radius, color ?? Colors.White);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddPolyline(this in RenderRid r, Vector2[] points, Color[]? color = null)
		{
			CanvasItemAddPolyline(r.Rid, points, color ?? [Colors.White]);
			return r;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddCircleOutline(this in RenderRid r, float radius, Vector2? position = null, Color? color = null, int segments = 32)
		{
			var points = new Vector2[segments + 1];
			var center = position ?? Vector2.Zero;

			for (int i = 0; i <= segments; i++)
			{
				float angle = i * Mathf.Tau / segments;
				points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			}

			return AddPolyline(r, points, [color?? Colors.White]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid AddRectOutline(this in RenderRid r, in Vector2 vec2, Color? color = null)
		{
			var rect = new Rect2(-vec2 / 2, vec2);
			var points = new[]
			{
				rect.Position,
				new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y),
				rect.Position + rect.Size,
				new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y),
				rect.Position
			};

			return AddPolyline(r, points, [color?? Colors.White]);
		}

		#endregion

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid Clear(this in RenderRid r)
		{
			CanvasItemClear(r.Rid);
			return r;
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderRid AddChildren(this in RenderRid r, RenderRid[] children)
        {
            foreach (var renderRid in children)
            {
                renderRid.SetParent(r);
            }

            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderRid AddChild(this in RenderRid r, RenderRid child)
        {
            child.SetParent(r);
            return r;
        }

	}

	public struct RenderRid(Rid rid)
	{
		public Rid Rid = rid;

		/// <summary>
		/// Creates a new canvas item using the RenderingServer and returns it as a RenderRid.
		/// </summary>
		/// <returns>A new <see cref="RenderRid"/> instance wrapping the created canvas item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid Create() => new(CanvasItemCreate());

		/// <summary>
		/// Creates a new canvas item using the RenderingServer and returns it as a RenderRid.
		/// </summary>
		/// <param name="parent">The parent canvas item under which this item will be created.</param>
		/// <param name="lifetime"></param>
		/// <returns>A new <see cref="RenderRid"/> instance wrapping the created canvas item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RenderRid Create(Rid parent, float? lifetime = null)
		{
			var v = new RenderRid(CanvasItemCreate());
			v.SetParent(parent);

			if (lifetime.HasValue)
				v.SetLifetime(lifetime.Value);

			return v;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Rid(RenderRid r) => r.Rid;
	}
}
