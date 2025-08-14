// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;

namespace XanaduProject.Audio
{
	public partial class NoteProcessor(
		Character.PlayerCharacter playerCharacter,
		EntityStore entityStore) : Node2D
	{
		/*	private float endTime;

			public override void _Ready()
			{
				GD.Print(OS.GetExecutablePath().GetBaseDir().PathJoin("Stages") );

			}

			public override void _Process(double delta)
			{
				base._Process(delta);

				if (!(endTime  + 2 < trackHandler.TrackPosition)) return;
				var devTemp = deviations;
				var judgeTemp = judgements;
				trackHandler.StopTrack();
				canvasLayer.AddChild(Results.Create(devTemp.ToArray(), judgeTemp.ToArray()));
			}
			}*/
	}
}
