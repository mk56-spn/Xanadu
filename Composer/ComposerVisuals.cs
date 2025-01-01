// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using static XanaduProject.ECSComponents.Presets.PrefabEntity;
using static XanaduProject.ECSComponents.RectEcs;

namespace XanaduProject.Composer
{
	public partial class ComposerVisuals : Control
	{
		private ComposerRenderMaster composer = null!;

		[Export] private Control editWidget = null!;
		[Export] private GridContainer gridContainer = null!;
		[Export] private VBoxContainer container = null!;
		[Export] private Button snap = null!;
		[Export] private Slider trackPos = null!;

		private Label infoLabel = new() { Visible = false, Modulate = Colors.GreenYellow };

		private Viewport viewport = null!;

		[Export] private Control waveformContainer = null!;
		public override void _EnterTree()
		{
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


			Vector2 mouse = composer.GetGlobalMousePosition();

			composer.Action = c =>
				Block(c, position(PRESETS[0]), PRESETS[0]);

			createButton("Hurt", c => Hurt(c, position(PRESETS[0]),PRESETS[0], composer.GetWorld2D()));
			createButton("Note", c => createNote(c, LARGE));
			createButton("Polygon", c => Polygon(c, position(new Vector2(32,32)), PolygonEcs.DEFAULT_POINTS));

			foreach (var extent in PRESETS)
					createButton(extent.ToString(), c => Block(c, position(extent), extent));
			return;

			Vector2 position(Vector2 size) { return (composer.GetGlobalMousePosition() + size / 2).Snapped(size) - size / 2; }

		}

		private void createNote(Entity c, Vector2 size)
		{
			Vector2 mouse = composer.GetLocalMousePosition();
			Note(c, (mouse + size / 2).Snapped(size) - size / 2, (float)Mathf.Snapped(composer.TrackHandler.TrackPosition, 60 / 200f), NoteType.Up, composer.GetWorld2D());
		}


		private void createRect(Entity c, Vector2 size)
		{
			Vector2 mouse = composer.GetLocalMousePosition();
			Rect(c, (mouse + size / 2).Snapped(size) - size / 2, size);
		}

		private void createButton(string name, Action<Entity> action)
		{

			var button = new Button { Text = name };
			button.Pressed += () => composer.Action = action;
			container.AddChild(button);
		}

		public override void _Process(double delta)
		{
			infoLabel.Text = $"Canvas transform: {viewport.CanvasTransform.Origin} " +
							 $"\nSelected count: {composer.Selected.Count} " +
							 $"\nZoom: {viewport.CanvasTransform.Scale}";
		}

		public override void _PhysicsProcess(double delta)
		{
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
				(ref NoteEcs component1, ref ElementEcs element, Entity entity) =>
				{
					DrawSetTransformMatrix(element.Transform.Scaled(viewport.CanvasTransform.Scale).Translated(viewport.CanvasTransform.Origin));
					DrawArc(Vector2.Zero, NoteEcs.RADIUS, 0,Mathf.Pi * 2, 10, Colors.White);
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
