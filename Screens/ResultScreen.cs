// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Buttons;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.Stage;

namespace XanaduProject.Screens
{
    public partial class ResultScreen : Screen
    {
        private VBoxContainer info = new();
        private HBoxContainer buttons = new();
        private AnimatedHoverButton restart = new("Restart");
        private AnimatedHoverButton menu = new("Go to menu");
        public ResultScreen(EntityStore store)
        {
            Color = Colors.Red;
            restart.Pressed += () =>
            {
                var v = store.GetCommandBuffer();

                store.Query<NoteEcs>().ForEachEntity((ref NoteEcs component1, Entity entity) =>
                {
                    v.RemoveComponent<Judged>(entity.Id);
                    v.RemoveComponent<Hit>(entity.Id);
                });
                ScreenManager.RequestChangeScreen(new Player(store,
                        GD.Load<TrackInfo>("res://Resources/TestTrack.tres"))
                    , TransitionType.Fade);

                v.Playback();
            };

            menu.Pressed += () => ScreenManager.RequestChangeScreen(new MainMenu(), TransitionType.Fade);

            AddChild(buttons);
            buttons.AddChild(menu);
            buttons.AddChild(restart);

            buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            AddChild(info);
            info.AddThemeConstantOverride("separation", 20);
            buttons.AddThemeConstantOverride("separation", 30);

            setupTally(store);
            setupAccuracyLabel(store);
        }

        private void setupTally(EntityStore store)
        {
            foreach (var j in Enum.GetValues<Judgement>())
            {
                ResultText result = new ResultText();
                PanelContainer panel = new PanelContainer();
                panel.AddChild(result);
                info.AddChild(panel);
                result.Text = JudgementInfo.GetJudgmentText(j).Capitalize() + " : "  + store.Query<NoteEcs>().Entities.Count(c => c.GetComponent<Judged>().Judgement == j);

                result.LabelSettings.FontColor = JudgementInfo.GetJudgmentColor(j);
            }
        }

        private void setupAccuracyLabel(EntityStore store)
        {
            int totalNotes = store.Query<NoteEcs>().Entities.Count();

            float acc = 0;
            foreach (var v in store.Query<NoteEcs>().Entities)
            {
                var judgement = v.GetComponent<Judged>().Judgement;
                switch (judgement)
                {
                    case Judgement.FlawlessP or Judgement.Flawless:
                        acc += 100f / totalNotes;
                        break;
                    case Judgement.Clean:
                        acc += 80f / totalNotes;
                        break;
                    case Judgement.Fair:
                        acc += 70f / totalNotes;
                        break;
                    case Judgement.Deficient:
                        acc += 50f / totalNotes;
                        break;
                }
            }
            info.AddChild(new ResultText{ Text = acc.ToString("0.00") + "%" });

        }

        private partial class ResultText : Label
        {
            private static readonly ParticleProcessMaterial part = new()
            {
                Gravity = new Vector3(100,0,0),
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(15,70,0),
                ColorRamp = ParticlesRidExtensions.FadeGradient,
                Color = new Color(1,1,1,0.5f),
            };

            public ResultText()
            {

                RenderRid.Create(GetCanvasItem())
                    .SetZIndex(-10)
                    .AddParticles(ParticlesRid.Create()
                        .SetAmount(10)
                        .SetLifetime(5)
                        .SetMesh(MeshFactory.CreateCircle(10).GetRid())
                        .SetProcessMaterial(part.GetRid()));

                LabelSettings = new LabelSettings
                {
                    Font = new FontVariation
                    {
                        SpacingTop = 5,
                        VariationEmbolden = 0.5f
                    },
                    FontSize = 50,
                };
            }
        }
    }
}
