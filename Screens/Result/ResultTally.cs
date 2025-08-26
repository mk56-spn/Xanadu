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
    public partial class ResultTally : VBoxContainer
    {
        public ResultTally(EntityStore store)
        {
            CustomMinimumSize = new Vector2(500, 0);
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            foreach (var j in Enum.GetValues<Judgement>())
            {
                var panel = new PanelContainer();
                AddChild(panel);

                var hbox = new HBoxContainer();
                panel.AddChild(hbox);

                var judgementLabel = new ResultText {
                    Text = JudgementInfo.GetJudgmentText(j).Capitalize() + " :",
                };

                hbox.AddChild(judgementLabel);

                var countLabel = new ResultText
                {
                    Text = store.Query<NoteEcs>().Entities.Count(c => c.GetComponent<Judged>().Judgement == j).ToString("D3"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                countLabel.Modulate = JudgementInfo.GetJudgmentColor(j);

                hbox.AddChild(countLabel);
            }
        }
    }
}
