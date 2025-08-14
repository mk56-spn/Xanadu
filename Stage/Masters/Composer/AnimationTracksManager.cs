// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.Composer.TrackVisualiser;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.GameDependencies;

namespace XanaduProject.Composer
{
	public partial class AnimationTracksManager : VBoxContainer
	{

		public const int SPACING = 200;
		private readonly IClock clock = DiProvider.Get<IClock>();

		private ScrollContainer scrollContainer = new() { CustomMinimumSize = new Vector2(0, 150) };
		private VBoxContainer trackContainer;

		public AnimationTracksManager(EntityStore entityStore, Container editContainer)
		{
			AddChild(new TopBarInfo(scrollContainer));
			SizeFlagsHorizontal = SizeFlags.ExpandFill;
			ClipContents = true;
			trackContainer = new VBoxContainer();
			AddChild(new TrackOverlay(scrollContainer) { ZIndex = 2 });
			AddChild(scrollContainer);
			scrollContainer.AddChild(trackContainer);

			var v = entityStore.Query<FloatArrayEcs, ColorArrayEcs>().ToEntityList();
			v.SortByEntityId(SortOrder.Descending);

			int i = 0;
			entityStore.Query<FloatArrayEcs, ColorArrayEcs>().ForEachEntity((ref FloatArrayEcs component1,
				ref ColorArrayEcs component2, Entity entity) =>
			{
				i++;
				trackContainer.AddChild(new ColorTrackVisualizer(editContainer)
					{
						Entity = entity,
						Index = i
					}
				);
			});

			trackContainer.CustomMinimumSize = new Vector2((float)clock.TrackLength * 200, 0);

			MouseFilter = MouseFilterEnum.Stop;
		}

		private partial class TrackOverlay(ScrollContainer container) : Control
		{
			private readonly IClock clock = DiProvider.Get<IClock>();

			public override void _Process(double delta)
			{
				base._Process(delta);
				QueueRedraw();
			}

			public override void _Draw()
			{
				DrawSetTransform(new Vector2(
					(float)(clock.PlaybackTimeSec * SPACING) - container.ScrollHorizontal +
					TrackVisualiser<Control>.OFFSET, -10));

				DrawRect(new Rect2(new Vector2(-10, -8), new Vector2(20, 8)), Colors.Orange);
				DrawColoredPolygon([new Vector2(-10, 0), new Vector2(0, 14), new Vector2(10, 0)], Colors.Orange);
				DrawLine(Vector2.Zero, Vector2.Down * SPACING, Colors.Orange);
			}
		}

		private partial class TopBarInfo : Control
		{
			private readonly ScrollContainer container;

			public TopBarInfo(ScrollContainer container)
			{
				this.container = container;
				SizeFlagsHorizontal = SizeFlags.ExpandFill;
				CustomMinimumSize = new Vector2(0, 30);
			}

			private Font font = ThemeDB.FallbackFont;

			public override void _Process(double delta)
			{
				base._Process(delta);

				Position = Vector2.Zero with { X = -container.ScrollHorizontal };
			}

			public override void _Draw()
			{
				DrawSetTransform(new Vector2(20, 20));

				for (int i = 0; i < 300; i++)
				{
					DrawLine(new Vector2(i * SPACING, 10), new Vector2(i * SPACING, 150),
						Colors.DarkGray with { A = 0.3F });
					var v = font.GetStringSize(i.ToString());
					DrawString(font, new Vector2(i * SPACING - v.X / 2, 0), i.ToString());
				}
			}
		}
	}
}
