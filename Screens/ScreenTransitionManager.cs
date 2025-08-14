// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Character;

namespace XanaduProject.Screens
{
    /// <summary>
    /// Manages animated screen transitions like fading, sliding, and scaling.
    /// </summary>
    public class ScreenTransitionManager(Control parent, float transitionDuration)
    {
        /// <summary>
        /// Starts the transition to hide the current screen.
        /// </summary>
        public void StartTransitionOut(Screen currentScreen, TransitionType transitionType)
        {
            var tween = parent.CreateTween();

            switch (transitionType)
            {
                case TransitionType.Fade:
                    tween.TweenProperty(currentScreen, "modulate:a", 0.0f, transitionDuration / 2);
                    break;

                case TransitionType.Slide:
                    tween.TweenProperty(currentScreen, "position:x", -parent.Size.X, transitionDuration);
                    break;

                case TransitionType.Scale:
                    tween.SetParallel();
                    tween.TweenProperty(currentScreen, "scale", new Vector2(2.0f, 2.0f), transitionDuration);
                    tween.TweenProperty(currentScreen, "modulate:a", 0.0f, transitionDuration);
                    break;
            }
        }

        /// <summary>
        /// Completes the transition by showing the next screen.
        /// </summary>
        public void CompleteTransition(Screen nextScreen, TransitionType transitionType, Action onCompleted)
        {
            var tween = parent.CreateTween();

            switch (transitionType)
            {
                case TransitionType.Fade:
                    nextScreen.Modulate = new Color(1, 1, 1, 0);
                    nextScreen.Visible = true;
                    // Animate the fade-in.
                    tween.TweenProperty(nextScreen, "modulate:a", 1.0f, transitionDuration / 2);
                    break;

                case TransitionType.Slide:
                    nextScreen.Position = new Vector2(parent.Size.X, 0);
                    nextScreen.Visible = true;
                    // Animate the slide-in.
                    tween.TweenProperty(nextScreen, "position:x", 0, transitionDuration);
                    break;

                case TransitionType.Scale:
                    nextScreen.Scale = new Vector2(0.1f, 0.1f);
                    nextScreen.Modulate = new Color(1, 1, 1, 0);
                    nextScreen.Visible = true;
                    // Animate the scale and fade-in.
                    tween.SetParallel()
                        .TweenProperty(nextScreen, "scale", Vector2.One, transitionDuration);
                    tween.TweenProperty(nextScreen, "modulate:a", 1.0f, transitionDuration);
                    break;
            }

            // Connect the onCompleted callback to the tween's completion signal.
                tween.Finished += onCompleted;
        }
    }

    public enum TransitionType
    {
        Fade,
        Slide,
        Scale
    }
}
