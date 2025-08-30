// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Animation;
using XanaduProject.Factories;
using XanaduProject.Tools;

namespace XanaduProject.Screens.Result
{
    public partial class ResultBackGround : PanelContainer
    {
        public ResultBackGround()
        {
            Modulate = Colors.White.Darkened(0.4f);

            AddChild(new TextureRect()
            {
                ShowBehindParent = true,
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
            this.DrawTransitionLine(Size.X, Size.Y - 100, Size.Y - 300, 250, Colors.White,  0.7f,  fill: true, fillColor: Colors.White.Darkened(0.8f));

            this.DrawTransitionLine(Size.X, 50,100, 150, Colors.Gold, 0.6f, fill: true, fillColor: Colors.Gold.Darkened(0.7f) , fillAbove: true);
            this.DrawTransitionLine(Size.X, Size.Y - 150, Size.Y - 100, 150, Colors.Gold,  0.4f,  fill: true, fillColor: Colors.Gold.Darkened(0.7f));

            canvas.SetTransform(new Transform2D(0, Size / 2));
        }
    }
}
