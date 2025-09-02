// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.ScreenStructure;
using Screen = XanaduProject.Screens.ScreenStructure.Screen;

namespace XanaduProject.Screens
{
    public partial class ScreenManager : Control
    {
        private readonly ScreenFader screenFader;
        private readonly SubScreenManager subScreenManager;

        private Control? background;
        private const float transition_duration = 0.5f;

        public ScreenManager()
        {
            var transitionManager1 = new ScreenTransitionManager(this, transition_duration);
            subScreenManager = new SubScreenManager(this, transitionManager1);
            DiProvider.Register(c => { c.AddSingleton(this); });

            screenFader = new ScreenFader(this, transitionManager1, OnScreenChanged, OnScreenCleanup);

            background = new ColorRect
            {
                Color = new Color(0.2f, 0.0f, 0.0f, 1.0f),
            };
            AddChild(background);

            RequestChangeScreen(new MainMenu(), TransitionType.Fade);
            setupParticles();
        }

        public void ChangeSubScreen(SubScreen screen)
        {
            subScreenManager.ChangeSubScreen(screen);
        }

        public void RemoveSubscreen()
        {
            subScreenManager.RemoveSubScreen();
        }

        public override void _Ready()
        {
            GameSettings.ApplyResolution();
            background?.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        public void RequestChangeScreen(Screen screen, TransitionType transitionType = TransitionType.Slide)
        {
            screenFader.ChangeScreen(screen, transitionType);
        }

        public async void RequestChangeScreen<T>(Func<T> screenFactory, TransitionType transitionType = TransitionType.Slide) where T : Screen
        {
            Screen screen = await Task.Run(screenFactory);
            screenFader.ChangeScreen(screen, transitionType);
        }

        private void OnScreenChanged(Screen newScreen)
        {

            if (newScreen.BackgroundOverride != null)
            {
                if (background != null)
                {
                    RemoveChild(background);
                    background.QueueFree();
                }
                background = newScreen.BackgroundOverride;
                AddChild(background);
                MoveChild(background, 0);
            }

            particleProcessMaterial.Color = newScreen.Color;
            RemoveSubscreen();
        }

        private void OnScreenCleanup(Screen oldScreen)
        {
            if (!IsInstanceValid(oldScreen)) return;

            if (oldScreen.BackgroundOverride != null)
            {
                RemoveChild(oldScreen.BackgroundOverride);
                oldScreen.BackgroundOverride.QueueFree();

                // Restore default background
                background = new ColorRect
                {
                    Color = new Color(0.2f, 0.0f, 0.0f, 1.0f),
                    Size = GetViewportRect().Size
                };
                AddChild(background);
                MoveChild(background, 0);
            }
            oldScreen.QueueFree();
        }

        #region Particles
        private readonly ParticleProcessMaterial particleProcessMaterial = new()
        {
            TurbulenceEnabled = true,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1000, 1000, 0),
            ScaleMax = 1f,
            ScaleMin = 0.4f,
            ColorRamp = new GradientTexture1D
            {
                Gradient = new Gradient
                {
                    Offsets = new[] { 0, 0.5f, 1 },
                    Colors = new[] { Colors.Transparent, Colors.White, Colors.Transparent }
                }
            },
            ColorInitialRamp = new GradientTexture1D
            {
                Gradient = new Gradient
                {
                    Offsets = [0f, 1f],
                    Colors = [Colors.Transparent, Colors.White]
                }
            }
        };

        private void setupParticles()
        {
            var canvas = RenderRid.Create(GetCanvasItem())
                .SetTransform(new Transform2D(0, new Vector2(1000, 1000)));
            canvas.AddParticles(ParticlesRid.Create()
                .SetAmount(1000)
                .SetLifetime(100)
                .SetMesh(MeshFactory.CreateStar(4, 10, 0.5f).GetRid())
                .SetProcessMaterial(particleProcessMaterial.GetRid()));
        }
        #endregion
    }
}
