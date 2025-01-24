// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;

namespace XanaduProject.Composer
{
	public partial class ComposerVisuals : Control
	{
		private ComposerRenderMaster composer = null!;
		private Label infoLabel = new() { Visible = false, Modulate = Colors.GreenYellow };
		private Viewport viewport = null!;

		[Export] private Control editWidget = null!;
		[Export] private ButtonContainer buttonContainer = null!;
		[Export] private Button snap = null!;
		[Export] private Slider trackPos = null!;
		[Export] private Container keyframeTrackContainer = null!;
		[Export] private Control waveformContainer = null!;

		public override void _EnterTree()
		{
			editWidget.AddChild(ComposerEditWidget.Create(composer));
			waveformContainer.AddChild(new Waveform(composer));
			GetNode<Button>("%Toggle").Pressed += () => composer.TrackHandler.TogglePlayback();
			GetNode<Button>("%Stop").Pressed += () => composer.TrackHandler.StopTrack();

			buttonContainer.Composer = composer;
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
			keyframeTrackContainer.AddChild(new AnimationTracksManager(composer.EntityStore, composer.TrackHandler));

			snap.Pressed += () => composer.Snapped = !composer.Snapped;
			viewport = GetViewport();
			AddChild(infoLabel);
			infoLabel.Position = new Vector2(30, 100);
			SetAnchorsPreset(LayoutPreset.FullRect);
			composer.Ready += () => composer.AddChild(new Grid { ShowBehindParent = true });

			trackPos.MaxValue = composer.TrackHandler.TrackLength;
			trackPos.Step = 0.01f;
			trackPos.ValueChanged += value =>
			{
				bool toggled = composer.TrackHandler.Playing;
				composer.TrackHandler.SetPos((float)value);
				if (toggled == false) composer.TrackHandler.TogglePlayback();
			};
		}

		public override void _Process(double delta)
		{
			infoLabel.Text = $"Canvas transform: {viewport.CanvasTransform.Origin} " +
							 $"\nSelected count: {composer.Selected.Count} " +
							 $"\nZoom: {viewport.CanvasTransform.Scale}";
			QueueRedraw();
		}

		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventKey { KeyLabel: Key.F10, Pressed: true })
				infoLabel.Visible = !infoLabel.Visible;

			if (@event is InputEventKey { KeyLabel: Key.Space, Pressed: true })
				composer.TrackHandler.TogglePlayback();
		}

		public override void _Draw()
		{
			composer.EntityStore.Query<NoteEcs, ElementEcs>().ForEachEntity(
				(ref NoteEcs _, ref ElementEcs element, Entity _) =>
				{
					DrawSetTransformMatrix(element.Transform.Scaled(viewport.CanvasTransform.Scale).Translated(viewport.CanvasTransform.Origin));
					DrawArc(Vector2.Zero, NoteEcs.RADIUS, 0,Mathf.Pi * 2, 30, Colors.White);
				});
			composer.Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity entity) =>
			{
				DrawSetTransformMatrix(element.Transform.Scaled(viewport.CanvasTransform.Scale)
					.Translated(viewport.CanvasTransform.Origin));

				if (entity.HasComponent<NoteEcs>())
				{
					DrawCircle(Vector2.Zero, NoteEcs.RADIUS, ElementEcs.ComposerColour with { A = 0.3f });
					DrawArc(Vector2.Zero, NoteEcs.RADIUS, 0, Mathf.Tau, 50, ElementEcs.ComposerColour);
					return;
				}

				if (entity.HasComponent<RectEcs>())
				{
					var e = entity.GetComponent<RectEcs>().Extents;
					DrawRect(new Rect2(-e / 2, e), ElementEcs.ComposerColour with { A = 0.3f });
					DrawRect(new Rect2(-e / 2, e), ElementEcs.ComposerColour, false);
				}

				DrawString(ThemeDB.FallbackFont, Vector2.Zero, element.Transform.Origin.ToString());
			});

			int i = 0;
			foreach (var en in composer.Selected.Entities)
			{

				DrawString(ThemeDB.FallbackFont, Vector2.Zero with{ Y = 30 + i * 20 }, en.Id.ToString());
				i++;
			}
		}

		private partial class Grid : Node2D
		{
			private int lineCount = 100;
			private int spacing = 32;

			public override void _Process(double delta)
			{
				Position = GetViewport().GetCamera2D().Offset.Snapped(new Vector2(32, 32)) - new Vector2(1600, 1600);
			}

			public override void _Draw()
			{
				var lineY = new Vector2[lineCount * 2];
				var lineX = new Vector2[lineCount * 2];


				for (int i = 0; i < lineCount; i++)
				{
					lineY[i * 2] = new Vector2(i * spacing, 0);
					lineY[i * 2 + 1] = new Vector2(i * spacing, 3000);
					lineX[i * 2] = new Vector2(0, i * spacing);
					lineX[i * 2 + 1] = new Vector2(5000, i * spacing);
				}

				DrawMultiline(lineX, Colors.White with { A = 0.1f }, -1);
				DrawMultiline(lineY, Colors.White with { A = 0.1f }, -1);
			}
		}
	}
}
