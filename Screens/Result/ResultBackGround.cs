// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Animation;
using XanaduProject.Factories;

namespace XanaduProject.Screens.Result
{
    public partial class ResultBackGround : Control
    {
        public ResultBackGround()
        {
            Modulate = Colors.Gold.Darkened(0.5f);
            int scale = 100;
            int ringWidth = 30;

            var canvas = RenderRid.Create(GetCanvasItem())
                .AddCircle(20);

            canvas.SetMaterial(RotationAnimator.MATERIAL.GetRid());

            for (int i = 1; i < 7; i++)
            {
                ArrayMesh mesh = MeshFactory.CreateCutoutRing(
                    i * scale + ringWidth + i * 10,
                    i * scale ,
                    50  + i * 10,
                    i / 2 + 1,
                    5,
                    10,
                    10,
                    15);
                canvas.AddCircleOutline(i * scale - 20, segments: 50 + i * 10, width: 3, color:Colors.White.Darkened(0.1f * i));
                canvas.AddMesh(mesh.GetRid(), modulate: Colors.White.Darkened(0.25f * i)
                ,transform: i % 2 == 0 ? Transform2D.FlipX : Transform2D.Identity);
            }
            SetAnchorsAndOffsetsPreset(LayoutPreset.Center);

            RenderRid.Create(GetCanvasItem())
                .AddRect(new Vector2(1000, 1000))
                .SetMaterial(UiMaterials.FLARE.GetRid());
        }

        public override void _Draw()
        {
            base._Draw();
        }
    }
}
