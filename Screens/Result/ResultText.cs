// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Factories;

namespace XanaduProject.Screens.Result
{
    public partial class ResultText : Label
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
                    ShadowColor = Colors.Black,
                    ShadowOffset = new Vector2(5,5),
                    Font = new FontVariation
                    {
                        SpacingTop = 5,
                        BaseFont = FontSource.PLASTIC
                    },
                    FontSize = 50,
                };
            }
        }
    }

