// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using Stateless;
using System;
using XanaduProject.Screens.ScreenStructure;

namespace XanaduProject.Screens
{
    public class ScreenFader
    {
        private enum State { Idle, Visible, Transitioning }
        private enum Trigger { Change, TransitionFinished }
        private record ChangeRequest(Screen? Screen, TransitionType TransitionType);

        private readonly StateMachine<State, Trigger> fsm;
        private readonly StateMachine<State, Trigger>.TriggerWithParameters<ChangeRequest> changeTrigger;

        private Screen? currentScreen;
        private readonly Node screenParent;
        private readonly ScreenTransitionManager transitionManager;
        private readonly Action<Screen?>? onScreenChanged;
        private readonly Action<Screen>? onScreenCleanup;

        public ScreenFader(Node screenParent, ScreenTransitionManager transitionManager, Action<Screen?>? onScreenChanged = null, Action<Screen>? onScreenCleanup = null)
        {
            this.screenParent = screenParent;
            this.transitionManager = transitionManager;
            this.onScreenChanged = onScreenChanged;
            this.onScreenCleanup = onScreenCleanup;

            fsm = new StateMachine<State, Trigger>(State.Idle);
            changeTrigger = fsm.SetTriggerParameters<ChangeRequest>(Trigger.Change);

            fsm.Configure(State.Idle)
                .OnEntry(() => setCurrentScreen(null))
                .Permit(Trigger.Change, State.Transitioning);

            fsm.Configure(State.Visible)
                .Permit(Trigger.Change, State.Transitioning);

            fsm.Configure(State.Transitioning)
                .OnEntryFrom(changeTrigger, request => performTransition(request.Screen, request.TransitionType))
                .PermitIf(Trigger.TransitionFinished, State.Visible, () => currentScreen != null)
                .PermitIf(Trigger.TransitionFinished, State.Idle, () => currentScreen == null)
                .Ignore(Trigger.Change);
        }

        public void ChangeScreen(Screen? newScreen, TransitionType transitionType)
        {
            if (newScreen == currentScreen)
            {
                return;
            }
            fsm.Fire(changeTrigger, new ChangeRequest(newScreen, transitionType));
        }

        private void performTransition(Screen? newScreen, TransitionType transitionType)
        {
            var screenToTransitionOut = currentScreen;

            void bringInNewScreen()
            {
                setCurrentScreen(newScreen);

                if (currentScreen != null)
                {
                    bool wasVisible = currentScreen.Visible;
                    currentScreen.Visible = false;
                    screenParent.AddChild(currentScreen);

                    if (wasVisible)
                    {
                        transitionManager.CompleteTransition(currentScreen, transitionType, () => fsm.Fire(Trigger.TransitionFinished));
                    }
                    else
                    {
                        fsm.Fire(Trigger.TransitionFinished);
                    }
                }
                else
                {
                    fsm.Fire(Trigger.TransitionFinished);
                }
            }

            if (screenToTransitionOut != null)
            {
                transitionManager.StartTransitionOut(screenToTransitionOut, transitionType, () =>
                {
                    screenParent.RemoveChild(screenToTransitionOut);
                    if (onScreenCleanup != null)
                    {
                        onScreenCleanup(screenToTransitionOut);
                    }
                    else
                    {
                        screenToTransitionOut.QueueFree();
                    }
                    bringInNewScreen();
                });
            }
            else
            {
                bringInNewScreen();
            }
        }

        private void setCurrentScreen(Screen? screen)
        {
            currentScreen = screen;
            onScreenChanged?.Invoke(currentScreen);
        }
    }
}
