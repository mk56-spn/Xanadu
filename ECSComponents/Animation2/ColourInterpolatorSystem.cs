// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.Tools;

namespace XanaduProject.ECSComponents.Animation2
{
	public class ColourInterpolatorSystem( EntityStore entityStore) : QuerySystem
	{
		private ArchetypeQuery<FloatArrayEcs,ColorArrayEcs,ActiveColourEcs> colorTrackQuery = null!;

		private readonly EntityStore entityStore = entityStore;

		protected override void OnAddStore(EntityStore store)
		{
			base.OnAddStore(store);


			colorTrackQuery = store.Query<FloatArrayEcs, ColorArrayEcs, ActiveColourEcs>();
			imageTexture = ImageTexture.CreateFromImage(image);
		}

		protected override void OnUpdate()
		{


			entityStore.Query<FloatArrayEcs, ColorArrayEcs>().WithoutAllComponents(ComponentTypes.Get<ActiveColourEcs>()).ForEachEntity(
				(ref FloatArrayEcs component1, ref ColorArrayEcs component2, Entity entity) =>
				{ CommandBuffer.AddComponent<ActiveColourEcs>(entity.Id); } );

			CommandBuffer.Playback();
		   colorTrackQuery.Each(new TrackColorLerp());

		   updateGpuTexture();
		}

		private static int size = 100;
		private readonly Image image = Image.CreateEmpty(size,1,false, Image.Format.Rgba8);
		private readonly Color[] colors = new Color[size];
		private ImageTexture imageTexture = null!;
		private readonly byte[] byteColors = new byte[size * 4];

		private void updateGpuTexture()
		{
			for (int i = 0; i < size; i++)
				colors[i] = Colors.Red;

			int c = 0;
			colorTrackQuery.ForEachEntity((ref FloatArrayEcs _, ref ColorArrayEcs _,
				ref ActiveColourEcs active, Entity entity) =>
			{
				colors[c] = active.Color;
				c++;
			});

			for (int i = 0; i < colors.Length; i++)
			{
				byteColors[i * 4 + 0] = (byte)(colors[i].R * 255);
				byteColors[i * 4 + 1] = (byte)(colors[i].G * 255);
				byteColors[i * 4 + 2] = (byte)(colors[i].B * 255);
				byteColors[i * 4 + 3] = (byte)(colors[i].A * 255);
			}
			image.SetData(size,1,false, Image.Format.Rgba8, byteColors);
			imageTexture.Update(image);
			RenderingServer.GlobalShaderParameterSet("colours_texture", imageTexture);
		}

		private readonly struct TrackColorLerp : IEach<FloatArrayEcs, ColorArrayEcs, ActiveColourEcs>
		{
			public void Execute(ref FloatArrayEcs floats, ref ColorArrayEcs colors, ref ActiveColourEcs active)
			{
			   active.Color = lerpedFrameValue<Color>((float)GlobalClock.Instance.PlaybackTimeSec, floats.Points.AsSpan(), colors.Colors.AsSpan(), floats.Easing.AsSpan());
			}
		}
		private static T lerpedFrameValue<T>(float currentTime, ReadOnlySpan<float> timingPoints,ReadOnlySpan<T>  values, ReadOnlySpan<EasingType> easingTypes)
	   {
		   if (timingPoints.Length == 0)
			   return default!;

		   if (currentTime <= timingPoints[0])
			   return values[0];
		   if (currentTime >= timingPoints[^1])
			   return values[^1];

		   int index = timingPoints.BinarySearch(currentTime);
		   if (index < 0) index = ~index;

		   int prevIndex = index - 1;
		   int nextIndex = index;

		   float t = (currentTime - timingPoints[prevIndex]) / (timingPoints[nextIndex] - timingPoints[prevIndex]);

		   t = EasingFunctions.GetEasing(EasingType.Linear, t);

		   var interpolate = InterpolatorCache<T>.LERP;

		   return interpolate(values[prevIndex], values[nextIndex], t);
	   }
	}
}
