// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Animation;
using XanaduProject.Factories;

namespace XanaduProject.Screens.Result
{
    public partial class ResultBackGround : PanelContainer
    {
        public ResultBackGround()
        {
            Modulate = Colors.Gold.Darkened(0.4f);

            AddChild(new TextureRect()
            {
                Texture = new GradientTexture1D()
                {
                    Gradient = new Gradient()
                    {
                        Offsets = [0, 1],
                        Colors = [Colors.White with { A = 0.5f }, Colors.Transparent]
                    }
                }
            });


            setupRings();
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        private RenderRid canvas;
        private void setupRings()
        {
            ColorRect rect = new ColorRect
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                ZIndex = -4,
                Color = Colors.White.Darkened(0.8f)
            };



            AddChild(rect);
            int scale = 100;
            int ringWidth = 30;
            canvas = RenderRid.Create(GetCanvasItem())
                .AddCircle(20);

            canvas.SetMaterial(RotationAnimator.MATERIAL.GetRid());

            for (int i = 1; i < 4; i++)
            {
                ArrayMesh mesh = MeshFactory.CreateCutoutRing(
                    i * scale + ringWidth + i * 10,
                    i * scale,
                    50 + i * 10,
                    i / 2 + 1);
                canvas.AddCircleOutline(i * scale - 20, segments: 50 + i * 10, width: 3, color: Colors.White.Darkened(0.1f * i));
                canvas.AddMesh(mesh.GetRid(), modulate: Colors.White.Darkened(0.25f * i)
                    , transform: i % 2 == 0 ? Transform2D.FlipX : Transform2D.Identity);
            }
        }

        public override void _Draw()
        {
            base._Draw();
            canvas.SetTransform(new Transform2D(0, Size / 2));

            int lineCount = 4;
            float lineLength = 60f;
            float spacing = 12f;
            float lineWidth = 2f;

            // Draw lines in the top-left corner
            for (int i = 0; i < lineCount; i++)
            {
                var color = i % 2 == 0 ? Colors.White : Colors.Black;
                // Horizontal lines
                DrawLine(new Vector2(0, i * spacing), new Vector2(lineLength, i * spacing), color, lineWidth);
                // Vertical lines
                DrawLine(new Vector2(i * spacing, 0), new Vector2(i * spacing, lineLength), color, lineWidth);
            }

            // Draw lines in the bottom-right corner
            for (int i = 0; i < lineCount; i++)
            {
                var color = i % 2 == 0 ? Colors.White : Colors.Black;
                // Horizontal lines
                DrawLine(new Vector2(Size.X, Size.Y - (i * spacing)), new Vector2(Size.X - lineLength, Size.Y - (i * spacing)), color, lineWidth);
                // Vertical lines
                DrawLine(new Vector2(Size.X - (i * spacing), Size.Y), new Vector2(Size.X - (i * spacing), Size.Y - lineLength), color, lineWidth);
            }
        }
    }
}
