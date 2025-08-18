
// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Character;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.Settings;

namespace XanaduProject.Screens
{
    public partial class ScreenManager : Control
    {
        private Screen currentScreen = null!;
        private Screen nextScreen = null!;
        private bool isTransitioning;
        private readonly ScreenTransitionManager transitionManager;
        private readonly SubScreenManager subScreenManager;

        private const float transition_duration = 0.5f;

        public ScreenManager()
        {
            transitionManager = new ScreenTransitionManager(this, transition_duration);
            subScreenManager = new SubScreenManager(this, transitionManager);
            DiProvider.Register(c => { c.AddSingleton(this); });
            RequestChangeScreen(new MainMenu(), TransitionType.Fade);
        }

        public void InvokeSetting()
        {
            var settingsScreen = new SettingsSubScreen();
            subScreenManager.ChangeSubScreen(settingsScreen);
        }

        public void RemoveSubscreen() {
            subScreenManager.RemoveSubScreen();
        }

        public override void _Ready()
        {
            //Apply saved resolution
            GameSettings.ApplyResolution();
        }

        public void RequestChangeScreen(Screen screen, TransitionType transitionType = TransitionType.Slide)
        {
            if (isTransitioning)
                return;

            nextScreen = screen;

            // Start transition immediately
            isTransitioning = true;
            transitionManager.StartTransitionOut(currentScreen, transitionType);

            // Setup the next screen before animation
            nextScreen.Visible = false;
            AddChild(nextScreen);

            // Complete the transition with the fade in
            transitionManager.CompleteTransition(nextScreen, transitionType, OnTransitionCompleted);
        }

        public async void RequestChangeScreen<T>(Func<T> screenFactory, TransitionType transitionType = TransitionType.Slide) where T : Screen
        {
            if (isTransitioning)
                return;

            // Start transition immediately
            isTransitioning = true;
            transitionManager.StartTransitionOut(currentScreen, transitionType);

            // Create the screen asynchronously while the fade out is happening
            Screen screen = await Task.Run(screenFactory);

            nextScreen = screen;

            // Setup the next screen before animation
            nextScreen.Visible = false;
            AddChild(nextScreen);

            // Complete the transition with the fade in
            transitionManager.CompleteTransition(nextScreen, transitionType, OnTransitionCompleted);
        }


        private void OnTransitionCompleted()
        {
            if (IsInstanceValid(currentScreen))
            {
                RemoveChild(currentScreen);
                currentScreen.QueueFree();
            }

            // Update current screen reference
            currentScreen = nextScreen;

            // Reset transition flag
            isTransitioning = false;
        }
    }
}
