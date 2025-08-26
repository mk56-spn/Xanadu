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
            foreach (var j in Enum.GetValues<Judgement>())
            {
                ResultText result = new ResultText();
                PanelContainer panel = new PanelContainer();
                panel.AddChild(result);
                AddChild(panel);
                int v = store.Query<NoteEcs>().Entities.Count(c => c.GetComponent<Judged>().Judgement == j);
                result.Text = JudgementInfo.GetJudgmentText(j).Capitalize() + " : " + v;

                result.LabelSettings.FontColor = JudgementInfo.GetJudgmentColor(j);
            }
        }
    }
}
