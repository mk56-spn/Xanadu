// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.GameDependencies;
using XanaduProject.Rendering;

namespace XanaduProject.Character
{
	public partial class CharacterVisuals(PlayerCharacter playerCharacter) : Node2D
	{
		private Node2D visuals = null!;
		private IClock clock = DiProvider.Get<IClock>();

		public override void _EnterTree()
		{
			base._EnterTree();
			visuals = GD.Load<PackedScene>("uid://c0artk3e0k6yt").Instantiate<Node2D>();
			AddChild(visuals);
		}

		public override void _Process(double delta)
		{
			foreach (var variable in NoteTypeUtils.ACTION_SHAPES.Where(var
						 => Input.IsActionJustPressed(var.text)))
			{
				if (clock.IsPaused) return;
				setupHitVisuals(variable.noteType);
			}

			if (playerCharacter.Velocity.X != 0)
				visuals.Scale = visuals.Scale with { X = Mathf.Sign(playerCharacter.Velocity.X) };
		}


		private Vector2 size = new(800, 800);

		private void setupHitVisuals(NoteType type)
		{
			GD.Print("setup");

			var transform = Transform2D.Identity with { Origin = playerCharacter.GlobalPosition };


			var rid = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetZIndex(rid, 30);
			RenderingServer.CanvasItemSetTransform(rid, transform);

			RenderingServer.CanvasItemAddRect(rid, new Rect2(-size / 2, size), Colors.White);
			RenderingServer.CanvasItemSetParent(rid, DiProvider.Get<IVisualsMaster>().GameplayerLayerRid);
			RenderingServer.CanvasItemSetMaterial(rid, Materials.Hits.Get(HitShaderId.Default));
			RenderingServer.CanvasItemSetModulate(rid, type.NoteColor());
			RenderingServer.CanvasItemSetInstanceShaderParameter(rid, "hit_pos", clock.PlaybackTimeSec);


			if (type == NoteType.C) RenderingServer.CanvasItemSetInstanceShaderParameter(rid, "y_rot", 35f);


			cleanup(rid);
			clock.Stopped += () => RenderingServer.FreeRid(rid);
		}

		private async void cleanup(Rid rid)
		{
			double timeAtHit = clock.PlaybackTimeSec;

			while (clock.PlaybackTimeSec <= timeAtHit + 0.7)
				await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);

			RenderingServer.FreeRid(rid);
		}
	}
}
