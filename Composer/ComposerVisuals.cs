// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Composer
{
	public partial class ComposerVisuals : Control
	{
		private Label infoLabel = new() { Visible = false, Modulate = Colors.GreenYellow };

		private Viewport viewport = null!;

		private ComposerRenderMaster composer = null!;

		[Export]
		private GridContainer gridContainer = null!;
		[Export]
		private Control editWidget = null!;

		[Export]
		private Control waveformContainer = null!;

		public override void _EnterTree()
		{
			int i = 0;
			foreach (var texture in composer.SerializableStage.DynamicTextures)
			{
				Button b = new Button { Icon = texture as Texture2D , CustomMinimumSize = new Vector2(60,60)};
				b.ExpandIcon = true;
				gridContainer.AddChild(b);

				int i1 = i;
				b.Pressed += () => composer.SelectedTexture = i1;
				i++;
			}


			editWidget.AddChild(ComposerEditWidget.Create(composer));
			waveformContainer.AddChild(new Waveform(composer));
			GetNode<Button>("%Toggle").Pressed += () => composer.TrackHandler.TogglePlayback();
			GetNode<Button>("%Stop").Pressed += () => composer.TrackHandler.StopTrack();
		}

		public static ComposerVisuals Create(ComposerRenderMaster composer)
		{
			var visuals = GD.Load<PackedScene>("res://Composer/ComposerVisuals.tscn")
				.Instantiate<ComposerVisuals>();
			visuals.composer = composer;
			return visuals;
		}
		public override void _Ready()
		{
			viewport = GetViewport();
			AddChild(infoLabel);
			infoLabel.Position = new Vector2(30, 100);
			SetAnchorsPreset(LayoutPreset.FullRect);
			composer.Ready += () =>	composer.AddChild(new Grid() { ShowBehindParent = true });
		}

		public override void _Process(double delta)
		{
			infoLabel.Text = $"Canvas transform: {viewport.CanvasTransform.Origin} " +
							 $"\nSelected count: {composer.SelectedAreas.Count} " +
							 $"\nZoom: {viewport.CanvasTransform.Scale}";
		}

		public override void _PhysicsProcess(double delta) => QueueRedraw();

		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventKey { KeyLabel: Key.F10 , Pressed: true })
				infoLabel.Visible = !infoLabel.Visible;
		}

		public override void _Draw()
		{
			Vector2 center = Vector2.Zero;

			foreach (var pair in composer.SelectedAreas)
			{
				DrawSetTransformMatrix(pair.renderElement.Element.Transform.Scaled(viewport.CanvasTransform.Scale).Translated(viewport.CanvasTransform.Origin));

				Element element = pair.Item1.Element;
				center += element.Position;
				switch (element)
				{
					case NoteElement:
						DrawCircle(Vector2.Zero, NoteElement.RADIUS, element.ComposerColour with { A = 0.3f });
						DrawArc(Vector2.Zero, NoteElement.RADIUS, 0, Mathf.Tau, 50, element.ComposerColour);
						break;

					default:
						DrawRect(new Rect2(-element.Size() / 2, element.Size()), element.ComposerColour with { A = 0.3f });
						DrawRect(new Rect2(-element.Size() / 2, element.Size()),  element.ComposerColour, false);
						break;
				}
			}

			DrawSetTransformMatrix(Transform2D.Identity);
		}
		private partial class Grid : Node2D
		{
			private int spacing = 32;
			private int lineCount = 100;

			public override void _Process(double delta)
			{
				Position = GetViewport().GetCamera2D().Offset.Snapped(new Vector2(32, 32)) - new Vector2(1600, 1600);
			}

			public override void _Draw()
			{
				Vector2[] lineY = new Vector2[lineCount * 2];
				Vector2[] lineX = new Vector2[lineCount * 2];

				DrawSetTransform(-new Vector2(16, 16));
				for (int i = 0; i < lineCount; i++)
				{
					lineY[i * 2] = new Vector2(i * spacing, 0);
					lineY[i * 2 +1] = new Vector2(i * spacing, 3000);
					lineX[i * 2] = new Vector2(0, i * spacing);
					lineX[i * 2 +1] = new Vector2(5000, i * spacing);
				}
				DrawMultiline(lineX, Colors.White with { A = 0.02f }, -1);
				DrawMultiline(lineY, Colors.White with { A = 0.02f }, -1);
			}
		}
	}
}
