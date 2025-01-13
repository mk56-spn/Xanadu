// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Linq;
using System.Reflection;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents;

namespace XanaduProject.Composer
{
	public partial class ComposerEditWidget : Control
	{
		private ComposerRenderMaster composer = null!;

		[Export] private HBoxContainer container = null!;
		[Export] private VBoxContainer settersContainer = null!;

		private float time; // Keep track of time
		private ComposerEditWidget() { }

		public override void _Ready()
		{
			composer.SelectionChanged += () =>
			{
				GD.Print("SelectionChanged" + composer.Selected.Count);
				foreach (var child in settersContainer.GetChildren())
					child.QueueFree();

				foreach (var child in container.GetChildren())
					child.QueueFree();

				if (composer.Selected.Count != 1) return;


				foreach (var child in container.GetChildren().OfType<Notes>())
				{
					child.QueueFree();
				}

				if (composer.Selected.Entities.First().HasComponent<NoteEcs>())
				{
					container.AddChild(new Notes(composer.TrackHandler, composer.Selected.Entities.First()));
				}

				foreach (var component in composer.Selected.Entities.First().Components)
				{
					foreach (var field in component.Type.Type.GetFields()
								 .Where(m => m.GetCustomAttributes(typeof(ComposerAttribute), false).Length > 0))
					{
						switch (component.Type.Type)
						{
							case { } t when t == typeof(ElementEcs):
								uiControlSetup<ElementEcs>(field, composer.Selected.Entities.First());
								break;

							case { } t when t == typeof(RectEcs):
								if (composer.Selected.Count != 1) return;
								uiControlSetup<RectEcs>(field, composer.Selected.Entities.First());
								break;
							case { } t when t == typeof(NoteEcs):
								if (composer.Selected.Count != 1) return;

								composer.TrackHandler.SetPos(composer.Selected.Entities.First().GetComponent<NoteEcs>().TimingPoint);
								composer.TrackHandler.TogglePlayback();
								uiControlSetup<NoteEcs>(field, composer.Selected.Entities.First());
								break;
						}
					}
				}
			};
		}

		private void uiControlSetup<T>(FieldInfo info, Entity target) where T : struct, IComponent
		{
			Node? control = info.FieldType switch
			{
				{ } t when t == typeof(bool) => new CheckButton(),
				{ } t when t == typeof(int) => new SpinBox(){ MinValue = -50, MaxValue = 50},
				{ } t when t == typeof(NoteType) => new OptionButton(),
				{ } t when t == typeof(Color) => new ColorPickerButton { CustomMinimumSize = new Vector2(40, 100) },
				_ => null
			};

			if (control == null) return;

			ComposerAttribute v = (info.GetCustomAttributes(typeof(ComposerAttribute), false).First() as ComposerAttribute)!;

			settersContainer.AddChild(new Label { Text = v.Name });
			GD.PrintRich(v.Name);
			settersContainer.AddChild(control);
			settersContainer.AddChild(new ColorRect{Color = Colors.White, CustomMinimumSize = new Vector2(0, 1)});

			switch (control)
			{
				case CheckButton checkBox:
					checkBox.SetPressed((bool)info.GetValue(target.GetComponent<T>())!);
					checkBox.Toggled += on => setValue(on);
					break;
				case SpinBox spinBox:
					spinBox.SetValueNoSignal((int)info.GetValue(target.GetComponent<T>())!);
					spinBox.ValueChanged += d => setValue((int)d);
					break;
				case ColorPickerButton colorPicker:
					colorPicker.Color = (Color)info.GetValue(target.GetComponent<T>())!;
					colorPicker.ColorChanged += c => setValue(c);
					break;
				case OptionButton optionButton:
					GD.PrintRich("[code][color=orange]Option button");
					optionButton.AddItem(NoteType.Left.ToString(), (int)NoteType.Left);
					optionButton.AddItem(NoteType.Right.ToString(), (int)NoteType.Right);
					optionButton.AddItem(NoteType.Up.ToString(), (int)NoteType.Up);
					optionButton.AddItem(NoteType.Down.ToString(), (int)NoteType.Down);
					optionButton.Selected = (int)info.GetValue(target.GetComponent<T>())!;
					optionButton.ItemSelected += i => setValue((NoteType)i);
					break;
			}

			return;

			void setValue(object on)
			{
				ref var elementEcs = ref target.GetComponent<T>();
				info.SetValueDirect(__makeref(elementEcs), on);
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


			if (key is { KeyLabel: Key.R })
			{
				GD.Print("LMAO");
				composer.Selected.ForEachEntity(
					(ref ElementEcs element, ref SelectionEcs _, Entity entity) =>
					{
						element.Transform = element.Transform.RotatedLocal(Mathf.Pi / 2);
					});
			}
			var direction = key.KeyLabel switch
			{
				Key.I => Vector2.Up,
				Key.K => Vector2.Down,
				Key.J => Vector2.Left,
				Key.L => Vector2.Right,
				_ => Vector2.Zero
			};
			if (direction != Vector2.Zero)
				composer.Selected.ForEachEntity(
					(ref ElementEcs element, ref SelectionEcs _, Entity _) =>
					{
						int i = key.IsShiftPressed() ? 32 : 64;

						var newPos = element.Transform with { Origin = element.Transform.Origin + direction * i };
						element.Transform = newPos;
					});
		}

		private partial class Notes(TrackHandler trackHandler, Entity entity) : HBoxContainer
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

			public override void _EnterTree()
			{
				base._EnterTree();

				foreach (float value in values)
				{
					var s = new Button
					{
						Text = value.ToString(CultureInfo.InvariantCulture)
					};

					s.Pressed += () =>
					{
						if (!entity.HasComponent<NoteEcs>()) return;

						ref var note = ref entity.GetComponent<NoteEcs>();
						note.TimingPoint = (float)(Mathf.Snapped(note.TimingPoint, 60 / trackHandler.Bpm * 0.25) +
												   60 / trackHandler.Bpm * value);
					};
					AddChild(s);
				};
			}
		}
	}
}
