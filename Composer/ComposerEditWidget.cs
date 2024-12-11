// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox.Schema.JSON;
using Godot;
using XanaduProject.ECSComponents;

namespace XanaduProject.Composer
{
	public partial class ComposerEditWidget : Control
	{
		private ComposerRenderMaster composer = null!;

		[Export]
		private HBoxContainer container = null!;
		[Export]
		private VBoxContainer settersContainer = null!;
		private ComposerEditWidget() { }

		private Entity target;

		private Entity targetExposed
		{
			set
			{
				foreach (var child in settersContainer.GetChildren())
					child.QueueFree();

				target = value;

				foreach (var info in typeof(ElementEcs).GetFields()
							 .Where(m => m.GetCustomAttributes(typeof(ComposerAttribute), false).Length > 0))
					uiControlSetup(info);
			}
		}

		private void uiControlSetup(FieldInfo info)
		{
			if (info.FieldType == typeof(int))
			{
				SpinBox s = new SpinBox();
				s.SetValueNoSignal((double)info.GetValue(target.GetComponent<ElementEcs>())!);
				settersContainer.AddChild(s);
				s.ValueChanged += d => {
					ref ElementEcs elementEcs = ref target.GetComponent<ElementEcs>();
					info.SetValueDirect(__makeref(elementEcs), (int)d);
				};
			}

			if (info.FieldType == typeof(Color))
			{
				ColorPickerButton colorPicker = new ColorPickerButton() { CustomMinimumSize = new Vector2(40, 100) };

				colorPicker.Color = (Color)info.GetValue(target.GetComponent<ElementEcs>())!;
				settersContainer.AddChild(colorPicker);
				colorPicker.ColorChanged += c => {
					ref ElementEcs elementEcs = ref target.GetComponent<ElementEcs>();
					info.SetValueDirect(__makeref(elementEcs), c);
					target.GetComponent<ElementEcs>().UpdateCanvas();
				};
			}
		}
		public static ComposerEditWidget Create(ComposerRenderMaster composer)
		{
			var widget = GD.Load<PackedScene>("res://Composer/ComposerEditWidget.tscn")
				.Instantiate<ComposerEditWidget>();
			widget.composer = composer;
			return widget;
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is not InputEventKey { Pressed: true } key) return;

			int spacing = 32;

			var direction = key.KeyLabel switch
			{
				Key.Up => Vector2.Up,
				Key.Down => Vector2.Down,
				Key.Left => Vector2.Left,
				Key.Right => Vector2.Right,
				_ => Vector2.Zero
			};


			if (direction != Vector2.Zero){}

		}

		public override void _Process(double delta)
		{

			if (target != composer.Selected.Entities.FirstOrDefault())
				targetExposed = composer.Selected.Entities.FirstOrDefault();
			Visible = composer.Selected.Count != 0;
		}

		public override void _EnterTree() =>
			linkNoteButtons();

		private void linkNoteButtons()
		{
			float[] values =
			[
				-1,
				-0.5f,
				-0.25f,
				0.25f,
				0.5f,
				1
			];

			foreach (var noteButton in container.GetChildren().OfType<Button>())
				noteButton.Pressed += () =>
				{
					if (!target.HasComponent<NoteEcs>()) return;

					ref var note = ref target.GetComponent<NoteEcs>();
					note.TimingPoint = (float)(Mathf.Snapped(note.TimingPoint, 60 / composer.TrackHandler.Bpm * 0.25) +
											   60 / composer.TrackHandler.Bpm * values[noteButton.GetIndex()]);

				};
		}
	}
}
