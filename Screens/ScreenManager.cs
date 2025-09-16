// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.Singleton;
using XanaduProject.Tools;
using Screen = XanaduProject.Screens.ScreenStructure.Screen;

namespace XanaduProject.Screens
{
    public partial class ScreenManager : Control
    {
        private readonly ScreenFader screenFader;
        private readonly SubScreenManager subScreenManager;

        private Control? background;
        private const float transition_duration = 0.5f;

        private static readonly DefaultBackground default_background = new();

        public ScreenManager()
        {
            var transitionManager1 = new ScreenTransitionManager(this, transition_duration);
            subScreenManager = new SubScreenManager(this, transitionManager1);
            DiProvider.Register(c => { c.AddSingleton(this); });

            screenFader = new ScreenFader(this, transitionManager1, OnScreenChanged, OnScreenCleanup);

            updateBackground(null);

            RequestChangeScreen(new OpeningScreen());
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
        }

        public override void _EnterTree()
        {
            GodotTree.Setup(this);
            Ready += Logger.Boot;
        }


        public void RequestChangeScreen(MainScreen screen, TransitionType transitionType = TransitionType.Fade)
        {
            RemoveSubscreen();
            screenFader.ChangeScreen(screen, transitionType);
        }

        public async void RequestChangeScreen<T>(Func<T> screenFactory, TransitionType transitionType = TransitionType.Slide) where T : Screen
        {
            RemoveSubscreen();
            Screen screen = await Task.Run(screenFactory);
            screenFader.ChangeScreen(screen, transitionType);
        }

        private void OnScreenChanged(Screen newScreen)
        {
            updateBackground(newScreen.BackgroundOverride);
            particleProcessMaterial.Color = newScreen.Color;
        }

        private void OnScreenCleanup(Screen oldScreen)
        {
            if (!IsInstanceValid(oldScreen)) return;

            oldScreen.QueueFree();
        }

        private void updateBackground(Control? newBackground)
        {
            var targetBackground = newBackground ?? default_background;
            if (targetBackground == background)
                return;

            if (background != null)
            {
                RemoveChild(background);
                if (background != default_background)
                    background.QueueFree();
            }

            background = targetBackground;
            AddChild(background);
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MoveChild(background, 0);
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
                    Offsets = [0, 0.5f, 1],
                    Colors = [Colors.Transparent, Colors.White, Colors.Transparent]
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
