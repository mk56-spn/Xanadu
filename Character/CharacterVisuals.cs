// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Character
{
	public partial class CharacterVisuals(PlayerCharacter playerCharacter) : Node2D
	{
		private Node2D visuals = null!;

		public override void _EnterTree()
		{
			visuals = GD.Load<PackedScene>("uid://c0artk3e0k6yt").Instantiate<Node2D>();
			AddChild(visuals);
		}

		public override void _Process(double delta)
		{
			if (playerCharacter.Velocity.X != 0)
				visuals.Scale = visuals.Scale with { X = Mathf.Sign(playerCharacter.Velocity.X) };
		}
	}
}
