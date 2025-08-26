// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.Screens.Result
{
    public partial class ResultTally : VBoxContainer
    {
        public ResultTally(EntityStore store)
        {
            CustomMinimumSize = new Vector2(500, 0);
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            foreach (var j in Enum.GetValues<Judgement>())
            {
                if (GetChildCount() != 0)
                {
                    AddChild(new ColorRect
                    {
                        CustomMinimumSize = new Vector2(0,2),
                        Modulate = Colors.White.Darkened(0.9f),
                        SizeFlagsHorizontal = SizeFlags.ExpandFill
                    });
                }
                var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                AddChild(hbox);


                var judgementLabel = new ResultText {
                    Text = JudgementInfo.GetJudgmentText(j).Capitalize(),
                };

                hbox.AddChild(judgementLabel);
                hbox.AddChild(new Control()
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                });

                var countLabel = new TallyResultText
                {
                    Text = store.Query<NoteEcs>().Entities.Count(c => c.GetComponent<Judged>().Judgement == j).ToString("D3"),
                };
                countLabel.Modulate = JudgementInfo.GetJudgmentColor(j);

                hbox.AddChild(countLabel);
            }
        }

        private partial class TallyResultText : ResultText
        {
            private static ArrayMesh mesh = MeshFactory.CreateCutoutRing();
            public TallyResultText()
            {
                AddThemeStyleboxOverride("normal",new StyleBoxFlat
                {
                    Skew = new Vector2(0.25f,0),
                    ContentMarginLeft = 15,
                    ContentMarginRight = 20,
                    BorderWidthBottom = 2,
                    BorderWidthLeft = 3,
                    BorderWidthRight = 3,
                    BorderWidthTop = 2,
                    BgColor= Colors.White.Darkened(0.9f),
                    BorderColor = Colors.White.Darkened(0.5f)
                } );

            }

            public override void _Ready()
            {
                RenderRid.Create(GetCanvasItem())
                    .SetTransform(new Transform2D(0,Size / 2))
                    .AddRect(new Vector2(300, 500))
                    .SetModulate(Colors.White with { A = 0.3f})
                    .SetMaterial(UiMaterials.FLARE.GetRid());
            }
        }
    }
}
