// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;
using XanaduProject.Screens;

namespace XanaduProject.Character
{
	public partial class Screen : Control
	{
		public Screen()
		{
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}
		public ScreenManager ScreenManager { get; } = DiProvider.Get<Screens.ScreenManager>();
	}
}
