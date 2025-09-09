// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Stage
{
    public static class StageHelpers
    {
        public static Color GetStageDifficultyColour(int difficulty) =>
            difficulty switch
            {
                < 5 => Colors.GreenYellow,
                < 15 => Colors.Yellow,
                < 25 => Colors.Orange,
                < 35 => Colors.Red,
                _ => Colors.Red
            };

        public static string GetStageDifficultName(int difficulty)
        {
            return difficulty switch
            {
                < 5 => "Entry",
                < 15 => "Moderate",
                < 25 => "Advanced",
                < 35 => "Expert",
                _ => "Zenith"
            };
        }
    }
}
