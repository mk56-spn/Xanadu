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
                EmissionShapeOffset = new Vector3(-100,0,0),
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(15,30,0),
                ColorRamp = ParticlesRidExtensions.FadeGradient,
                Color = new Color(1,1,1,0.5f),
            };

            private QuadMesh mesh = new(){Size = new Vector2(50,3)};
            public ResultText()
            {

                LabelSettings = new LabelSettings
                {
                    OutlineSize = 10,
                    OutlineColor = Colors.White.Darkened(0.6F),
                    Font = new FontVariation
                    {
                        SpacingTop = 5,
                        BaseFont = FontSource.PLASTIC
                    },
                    FontSize = 35,
                };
                Material = FontSource.GRADIENT_MATERIAL;
            }
        }
    }

