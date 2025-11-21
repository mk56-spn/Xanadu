// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Tools;

namespace XanaduProject.Screens.Result
{
    public partial class ResultGraph(EntityStore store) : PanelContainer
    {
        public override void _Ready()
        {
            Control c = new Control();
            AddChild(c);
            Label label;
            c.AddChild(label = new Label
            {
                Text = "HITS",
                LabelSettings = new LabelSettings
                {
                    Font = FontSource.PLASTIC_SLANTED,
                    FontColor = Colors.Black
                }
            });
            label.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                ContentMarginTop = 5,
                ContentMarginBottom = 5,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                BgColor = Colors.Gold
            });
        }

        public override void _Draw()
        {
            CustomMinimumSize = new Vector2(500, 300);

            DrawSetTransform(new Vector2(0,CustomMinimumSize.Y / 2));

            foreach (var judgement in Enum.GetValues<Judgement>().Reverse())
            {
                var color = JudgementInfo.GetJudgmentColor(judgement).Darkened(0.3f);
                float offset = (float)JudgementInfo.JudgementDeviation(judgement);
                DrawRect(new Rect2(new Vector2(0, -offset), new Vector2(CustomMinimumSize.X, offset * 2)), color.Darkened(0.8f));
                DrawLine(new Vector2(0, offset), new Vector2(CustomMinimumSize.X,offset), color);
                DrawLine(new Vector2(0, -offset), new Vector2(CustomMinimumSize.X,-offset), color);
            }
            DrawLine(Vector2.Zero, new Vector2(CustomMinimumSize.X, 0), Colors.White);
            store.Query<Judged, NoteEcs>()
                .ForEachEntity((ref Judged judged,ref NoteEcs note, Entity _) =>
                {
                    DrawRect(new Rect2(new Vector2(note.TimingPoint *4,judged.Deviation), new Vector2(5,5)), JudgementInfo.GetJudgmentColor(judged.Judgement));
                });

            DrawSetTransform(default);
            DrawRect(new Rect2(Vector2.Zero, CustomMinimumSize), XanaduColors.XanaduYellow, false,2,antialiased: false);
            ;
        }
    }
}
