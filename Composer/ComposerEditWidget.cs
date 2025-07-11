// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;

namespace XanaduProject.Composer
{
	public partial class ComposerEditWidget : Control
	{
		private ComposerRenderMaster composer = null!;

		[Export] private HBoxContainer container = null!;
		[Export] private VBoxContainer settersContainer = null!;
		[Export] private GridContainer groups = null!;
		[Export] private GridContainer materials = null!;
		[Export] private Container note = null!;


		public override void _Ready()
		{
			updateTracks();
			updateMaterials();
			note.AddChild(new Notes(composer.EntityStore));
		}

		#region Tracks

		private void updateTracks()
		{
			int i = 0;

			composer.EntityStore.Query<FloatArrayEcs>().ForEachEntity((ref FloatArrayEcs _, Entity track) =>
			{
				Button b = new Button { Text = (i + 1).ToString()};

				b.FocusMode = FocusModeEnum.None;
				b.Pressed += () => updateIndex(track.Id -1);
				i++;

				groups.AddChild(b);
			});

		}

		private void updateIndex(int i)
		{

			CommandBuffer buffer = composer.EntityStore.GetCommandBuffer();
			composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).ForEachEntity(
				(ref ElementEcs _, Entity entity) =>
				{
					buffer.AddComponent(entity.Id, new TargetGroupEcs { Value = i});
				});

			buffer.Playback();
		}

		#endregion

		#region Materials

		private void updateMaterials()
		{
			BlockMaterialId[] mats = Enum.GetValues<BlockMaterialId>();

			foreach (var shader in mats)
			{
				Button b;
				materials.AddChild(b = new Button{Text = shader.ToString()});
				b.FocusMode = FocusModeEnum.None;
				b.Pressed += () => updateId(shader);

			}
		}

		private void updateId(BlockMaterialId id)
		{
			CommandBuffer buffer = composer.EntityStore.GetCommandBuffer();
			composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).ForEachEntity(
				(ref ElementEcs _, Entity entity) =>
				{
					buffer.AddComponent(entity.Id, new MaterialEcs{ Shader = id});
				});
			buffer.Playback();
		}

		#endregion

		#region Notes

		private partial class Notes(EntityStore store) : HBoxContainer
		{
			private float[] values =
			[
				-1,
				-0.5f,
				-0.25f,
				0.25f,
				0.5f,
				1
			];

			public override void _Process(double delta)=>
				Visible = store.Query<NoteEcs>().AllTags(Tags.Get<SelectionFlag>()).Count != 0;

			public override void _EnterTree()
			{
				foreach (float value in values)
				{
					var s = new Button {
						Text = value.ToString(CultureInfo.InvariantCulture)
					};

					s.Pressed += () => store.Query<NoteEcs>().Each(new EachNote(value));
					AddChild(s);
				}
			}

			private readonly struct EachNote(float value) : IEach<NoteEcs>
			{
				public void Execute(ref NoteEcs note)=>
					note.TimingPoint =
						(float)(Mathf.Snapped(note.TimingPoint, 60 / GlobalClock.Instance.CurrentBpm * 0.25) + 60 / GlobalClock.Instance.CurrentBpm* value);
			}
		}

		#endregion

		public static ComposerEditWidget Create(ComposerRenderMaster composer)
		{
			var widget = GD.Load<PackedScene>("res://Composer/ComposerEditWidget.tscn")
				.Instantiate<ComposerEditWidget>();
			widget.composer = composer;
			return widget;
		}
	}
}
