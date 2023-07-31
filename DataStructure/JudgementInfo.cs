// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;

namespace XanaduProject.DataStructure
{
    public struct JudgementInfo
    {
        /// <summary>
        /// Returns the judgement for the corresponding millisecond deviation on hitting the note.
        /// </summary>
        public static Judgement GetJudgement(double deviation) =>
            deviation switch
            {
                < 16 => Judgement.Flawless,
                < 27 => Judgement.Clean,
                < 35 => Judgement.Fair,
                < 70 => Judgement.Deficient,
                < 120 => Judgement.Terrible,
                _ => Judgement.Miss
            };

        /// <summary>
        /// Returns the text for the provided judgement.
        /// </summary>
        /// <returns></returns>
        public static string GetJudgmentText(Judgement judgement) =>
            judgement switch
            {
                Judgement.Flawless => "Flawless",
                Judgement.Clean => "Clean",
                Judgement.Fair => "Fair",
                Judgement.Deficient => "Deficient",
                Judgement.Terrible => "Terrible",
                Judgement.Miss => "Miss",
                _ => throw new ArgumentOutOfRangeException(nameof(judgement), judgement, null)
            };
    }

    public enum Judgement
    {
        Flawless,
        Clean,
        Fair,
        Deficient,
        Terrible,
        Miss
    }
}
