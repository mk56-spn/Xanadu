// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.Result
{
    public partial class ResultMisc : VBoxContainer
    {
        public ResultMisc(ScoreCalculator scoreCalculator)
        {
            AddChild(new InvertResultText
            {
                Text = "NOTES: " + scoreCalculator.TotalNotes,
            });
            AddChild(new InvertResultText
            {
                Text = "COMBO: " + scoreCalculator.MaxCombo,
            });

            AddThemeConstantOverride("separation", 0);

            AddChild(new InvertResultText
            {
                Text = $"UR: {scoreCalculator.UnstableRate:F2}"
            });
        }

        private partial class InvertResultText : ResultText
        {
            public InvertResultText()
            {
                AddThemeStyleboxOverride("normal", ResultStyles.INSET);

                LabelSettings.FontColor = Colors.Black;
                LabelSettings.OutlineSize = 0;
                LabelSettings.FontSize = 20;
            }
        }
    }
}
