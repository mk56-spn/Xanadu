// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.Screens.Result
{
    public partial class ResultTally : PanelContainer
    {
        private StyleBox style = new StyleBoxFlat()
        {
            BgColor = Colors.Black with { A = 0.2f },
            BorderColor = Colors.Gold,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            ContentMarginBottom = 20,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 20,
            AntiAliasingSize = 30,
            AntiAliasing = true,
        };
        public ResultTally(EntityStore store)
        {
            AddThemeStyleboxOverride("panel", style);
            CustomMinimumSize = new Vector2(500, 0);

            var mainHBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            AddChild(mainHBox);

            var namesVBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            mainHBox.AddChild(namesVBox);
            mainHBox.AddChild(new ColorRect
            {
                GrowHorizontal = GrowDirection.Both,
                CustomMinimumSize = new Vector2(4,0),
                Modulate = Colors.Gold,
            });
            var countsVBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            mainHBox.AddChild(countsVBox);

            foreach (var j in Enum.GetValues<Judgement>())
            {
                var judgementLabel = new ResultText {
                    Text = JudgementInfo.GetJudgmentText(j).Capitalize(),
                };
                namesVBox.AddChild(judgementLabel);

                var countLabel = new ResultTallyText()
                {
                    Text = store.Query<NoteEcs>().Entities.Count(c => c.GetComponent<Judged>().Judgement == j).ToString("D3"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                countsVBox.AddChild(countLabel);
            }

        }

        private partial class ResultTallyText : ResultText
        {
            public ResultTallyText()
            {
            }

            public override void _Process(double delta)
            {
                base._Process(delta);
                HorizontalAlignment = HorizontalAlignment.Center;

            }

        }
    }
}
