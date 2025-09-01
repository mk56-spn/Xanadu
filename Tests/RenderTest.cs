// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Stage;

namespace XanaduProject.Tests
{
	public partial class RenderTest : Screens.ScreenManager
	{

		[Export] private Type type = Type.Composer;

		private enum Type
		{
			Composer,
			Player
		}
		public override void _Ready()
        {
            var v = StagePersistence.GetStage(StageIndex.Stages.First().Value);
            var player = type == Type.Player ? new Player(v) : new Stage.Masters.Composer.Composer(v);

            AddChild(player);
		}
	}
}
