// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Factories;
using XanaduProject.Factories.ShaderFactoryHelpers;
using XanaduProject.Screens.Labels;
using XanaduProject.Stage;
using XanaduProject.Tools;

namespace XanaduProject.Screens.StageSelection
{
    public partial class InfoDisplayContainer : MarginContainer
    {
        private DifficultyLabel difficulty = new();
        private Label difficultyName = new()
        {
            LabelSettings = new LabelSettings()
            {
                Font = FontSource.LINE_BOLD,
                FontSize = 30,
            }
        };

        private HBoxContainer hBoxContainer = new();

        public void Update(StageInfo data)
        {
            difficulty.Update(data);
            difficultyName.Text = StageHelpers.GetStageDifficultName(data.Difficulty).ToUpper();

            var tween = CreateTween();
            tween.SetParallel();


            tween.TweenProperty(this, "modulate", Colors.Transparent, 0);
            tween.TweenProperty(this, "modulate", Colors.White , 0.2f).SetDelay(0.4f);
            QueueRedraw();
        }
        public InfoDisplayContainer()
        {
            CustomMinimumSize = new Vector2(400, 0);
            AddThemeConstantOverride("margin_top", 10);
            AddThemeConstantOverride("margin_right", 10);
            AddThemeConstantOverride("margin_bottom", 10);
            AddThemeConstantOverride("margin_left", 20);

            AddChild(hBoxContainer);
            hBoxContainer.AddChild(difficulty);
            hBoxContainer.AddChild(new Control() { CustomMinimumSize = new Vector2(10,0)});
            hBoxContainer.AddChild(difficultyName);

            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, margin: 301);
        }

        public override void _Draw()
        {
                this.DrawSquareWithInsets(new Rect2(Vector2.Zero, Size),
                new Inset
                {
                    InnerLength = Size.X,
                    OuterLength = 60,
                    InsetDepth = 10,
                    InsetCurveTension = 0,
                },
                new Bevel
                {
                    BevelRadius = 10,
                    BevelSides = Sides.All
                },
                new ShapeStyle
                {
                    Fill = true,
                    Outline = true,
                    FillColor = UiColours.GREY3,
                    OutlineColor = UiColours.GREY1,
                    OutlineWidth = 1
                });
        }

        private partial class DifficultyLabel : BoldLabel
        {
            public DifficultyLabel()
            {
                CustomMinimumSize = new Vector2(70, 50);
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

                HorizontalAlignment = HorizontalAlignment.Center;
                VerticalAlignment = VerticalAlignment.Center;

                LabelSettings.FontSize = 30;
            }

            public void Update(StageInfo data)
            {
                Text = data.Difficulty.ToString();

                Modulate = StageHelpers.GetStageDifficultyColour(data.Difficulty);
                canvas = RenderRid.Create(GetCanvasItem());

                rotator.SetShaderParameter(VertexShaders.ROTATION_SPEED, 0.5);
            }

            private RenderRid canvas;
            private ShaderMaterial rotator = new ShaderFactory().Add(VertexShaders.ROTATION_VERTEX).Build();

            public override void _Draw()
            {
                canvas.SetTransform(new Transform2D(0, Size / 2));
                this.DrawCircularGlow(Size / 2, 50, Colors.White with { A = 0.3f});
                this.DrawSquareWithInsets(new Rect2(Vector2.Zero, Size),
                    new Inset
                    {
                        InnerLength = Size.X,
                        OuterLength = 60,
                        InsetDepth = 10,
                        InsetCurveTension = 0,
                    },
                    new Bevel
                    {
                        BevelRadius = 10,
                        BevelSides = Sides.All
                    },
                    new ShapeStyle
                    {
                        Fill = true,
                        FillColor = UiColours.MAIN_COLOUR with{ A = 0.5f},
                        Outline = true,
                        OutlineColor = UiColours.MAIN_COLOUR,
                        OutlineWidth = 1
                    });
            }
        }
    }
}
