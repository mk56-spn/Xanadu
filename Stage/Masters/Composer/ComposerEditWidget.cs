// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Rendering;

namespace XanaduProject.Stage.Masters.Composer
{
	public partial class ComposerEditWidget : Control
	{
		[Export] private HBoxContainer container = null!;
		[Export] private VBoxContainer settersContainer = null!;
		[Export] private GridContainer groups = null!;
		[Export] private GridContainer materials = null!;
		[Export] private Container note = null!;

		private GridContainer blocks = new();

		private readonly IComposer iComposer = DiProvider.Get<IComposer>();
		private readonly IClock clock = DiProvider.Get<IClock>();

		public override void _Ready()
		{
			materials.AddChild(blocks);

			updateMaterials();
			note.AddChild(new Notes());
			groups.AddChild(new GroupContainer());
		}

		#region Materials

		private void updateMaterials()
		{
			var mats = Enum.GetValues<BlockShaderId>();

			foreach (var shader in mats)
			{
				Button b;
				blocks.AddChild(b = new Button { CustomMinimumSize = new Vector2(50, 50) });
				var r = RenderingServer.CanvasItemCreate();
				RenderingServer.CanvasItemSetTransform(r, new Transform2D(0, new Vector2(25, 25)));
				RenderingServer.CanvasItemSetParent(r, b.GetCanvasItem());
				RenderingServer.CanvasItemAddRect(r, new Rect2(new Vector2(-25, -25), new Vector2(50, 50)),
					Colors.White);
				RenderingServer.CanvasItemSetMaterial(r, Materials.Blocks.Get(shader));
				b.FocusMode = FocusModeEnum.None;
				b.Pressed += () => updateBlockId(shader);
			}
		}


		public override void _Process(double delta)
		{
			blocks.Visible =
				iComposer.EntityStore.Query<ElementEcs, RectEcs>().AllTags(Tags.Get<SelectionFlag>()).Count != 0;
		}

		private void updateBlockId(BlockShaderId id)
		{
			var buffer = iComposer.EntityStore.GetCommandBuffer();
			iComposer.EntityStore.Query<ElementEcs, RectEcs>().AllTags(Tags.Get<SelectionFlag>())
				.ForEachEntity((ref ElementEcs _, ref RectEcs rect, Entity entity) =>
				{
					buffer.AddComponent(entity.Id, new MaterialEcs { Shader = id });
				});
			buffer.Playback();
		}

		#endregion

		#region Notes

		private partial class Notes: HBoxContainer
		{
			private readonly IComposer iComposer = DiProvider.Get<IComposer>();

			private float[] values =
			[
				-1,
				-0.5f,
				-0.25f,
				0.25f,
				0.5f,
				1
			];

			public override void _Process(double delta)
			{
				Visible = iComposer.EntityStore.Query<NoteEcs>().AllTags(Tags.Get<SelectionFlag>()).Count != 0;
			}

			public override void _EnterTree()
			{
				foreach (float value in values)
				{
					var s = new Button
					{
						Text = value.ToString(CultureInfo.InvariantCulture)
					};

					s.Pressed += () =>
						iComposer.EntityStore.Query<NoteEcs>().AllTags(Tags.Get<SelectionFlag>()).Each(new EachNote(value));
					AddChild(s);
				}
			}

			private readonly struct EachNote(float value) : IEach<NoteEcs>
			{
				private readonly IClock clock = DiProvider.Get<IClock>();

				public void Execute(ref NoteEcs note)
				{
					note.TimingPoint =
						(float)(Mathf.Snapped(note.TimingPoint, 60 / clock.CurrentBpm * 0.25) +
								60 / clock.CurrentBpm * value);
				}
			}
		}

		#endregion

		public static ComposerEditWidget Load = GD.Load<PackedScene>("uid://cwvbay6iwo7h3").Instantiate<ComposerEditWidget>();
		public static ComposerEditWidget Create()
		{
			return Load;
		}

		private partial class GroupContainer : GridContainer
		{

			private readonly IComposer iComposer = DiProvider.Get<IComposer>();


			private readonly Dictionary<int, Button> trackButtons = new();
			private readonly HashSet<int> selectedTrackIndices = [];

			public GroupContainer()
			{
				Columns = 10;


				iComposer.EntityStore.Query<FloatArrayEcs>().ForEachEntity((ref FloatArrayEcs _, Entity track) =>
				{
					var b = new Button { Text = track.Id.ToString() };
					trackButtons.Add(track.Id, b);
					b.FocusMode = FocusModeEnum.None;

					b.Pressed += () => updateIndex(track.Id - 1);
					AddChild(b);
				});
			}

			private void updateIndex(int i)
			{
				var buffer = iComposer.EntityStore.GetCommandBuffer();
				iComposer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>())
					.ForEachEntity((ref ElementEcs _, Entity entity) =>
					{
						buffer.AddComponent(entity.Id, new TargetGroupEcs { Value = i });
					});

				buffer.Playback();
			}

			public override void _Process(double delta)
			{
				iComposer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>())
					.ForEachEntity((ref ElementEcs component1, Entity _) =>
					{
						// We return if the element has no index
						if (component1.Index == 0) return;
						selectedTrackIndices.Add(component1.Index);
					});

				foreach (var key in trackButtons)
					key.Value.Modulate = Colors.White;

				foreach (int selected in selectedTrackIndices)
					trackButtons[selected].Modulate = Colors.Green;
			}
		}
	}
}
