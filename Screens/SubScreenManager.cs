// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens
{
    public partial class SubScreenManager : Node
    {
        private readonly CanvasLayer screenTarget = new() { Layer = 5, };
        private readonly ScreenFader screenFader;

        public SubScreenManager(ScreenManager screenManager, ScreenTransitionManager transitionManager)
        {
            screenManager.AddChild(screenTarget);
            screenFader = new ScreenFader(screenTarget, transitionManager);
        }

        public void ChangeSubScreen(Screen? newSubScreen, TransitionType transitionType = TransitionType.Fade)
        {
            screenFader.ChangeScreen(newSubScreen, transitionType);
        }

        public void RemoveSubScreen(TransitionType transitionType = TransitionType.Fade)
        {
            ChangeSubScreen(null, transitionType);
        }
    }
}
