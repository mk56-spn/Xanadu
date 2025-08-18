// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Character;

namespace XanaduProject.Screens
{
    public partial class SubScreenManager : Node
    {
        private Screen? currentSubScreen;
        private bool isTransitioning;
        private readonly ScreenManager screenManager;
        private readonly ScreenTransitionManager transitionManager;

        public SubScreenManager(ScreenManager screenManager, ScreenTransitionManager transitionManager)
        {
            this.screenManager = screenManager;
            this.transitionManager = transitionManager;
        }

        /// <summary>
        /// Transitions from the current sub-screen to a new one.
        /// If a sub-screen is already active, it will be transitioned out before the new one is transitioned in.
        /// Passing null for <paramref name="newSubScreen"/> will remove the current sub-screen.
        /// </summary>
        /// <param name="newSubScreen">The new sub-screen to display, or null to remove the current one.</param>
        /// <param name="transitionType">The type of transition to use.</param>
        public void ChangeSubScreen(Screen? newSubScreen, TransitionType transitionType = TransitionType.Fade)
        {
            // Don't start a new transition if one is already in progress,
            // or if the new screen is the same as the current one.
            if (isTransitioning || newSubScreen == currentSubScreen)
            {
                return;
            }

            isTransitioning = true;

            // This local function handles bringing in the new screen after the old one is gone.
            void bringInNewScreen()
            {
                currentSubScreen = newSubScreen;

                if (currentSubScreen != null)
                {
                    // Prepare and add the new screen to the scene tree.
                    currentSubScreen.Visible = false;
                    screenManager.AddChild(currentSubScreen);

                    // Transition it into view.
                    transitionManager.CompleteTransition(currentSubScreen, transitionType, () => { isTransitioning = false; });
                }
                else
                {
                    // No new screen to show, so the transition is complete.
                    isTransitioning = false;
                }
            }

            if (currentSubScreen != null)
            {
                // If a subscreen is currently active, transition it out first.
                // Capture the current screen in a local variable for the lambda to avoid issues with shared state.
                var screenToTransitionOut = currentSubScreen;
                transitionManager.StartTransitionOut(screenToTransitionOut, transitionType, () =>
                {
                    screenManager.RemoveChild(screenToTransitionOut);
                    screenToTransitionOut.QueueFree();
                    bringInNewScreen();
                });
            }
            else
            {
                // If no subscreen is active, we can immediately bring in the new one.
                bringInNewScreen();
            }
        }

        public void RemoveSubScreen(TransitionType transitionType = TransitionType.Fade)
        {
            if (currentSubScreen == null) return;
            // Removing a subscreen is equivalent to changing to a null screen.
            ChangeSubScreen(null, transitionType);
        }
    }
}
