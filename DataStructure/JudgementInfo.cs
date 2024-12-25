// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;

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
                < 8.3 => Judgement.FlawlessP,
                < 16 => Judgement.Flawless,
                < 27 => Judgement.Clean,
                < 35 => Judgement.Fair,
                < 70 => Judgement.Deficient,
                < 120 => Judgement.Terrible,
                _ => Judgement.Miss
            };

        /// <summary>
        /// The max deviation a given judgement type can have
        /// </summary>
        /// <param name="judgement"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static double JudgementDeviation(Judgement judgement) =>
            judgement switch
            {
                Judgement.FlawlessP =>  8.3,
                Judgement.Flawless => 16,
                Judgement.Clean => 27,
                Judgement.Fair => 35,
                Judgement.Deficient => 70,
                Judgement.Terrible => 120,
                Judgement.Miss => 200,
                _ => throw new ArgumentOutOfRangeException(nameof(judgement), judgement, null)
            };
        /// <summary>
        /// Returns the text for the provided judgement.
        /// </summary>
        /// <returns></returns>
        public static string GetJudgmentText(Judgement judgement) =>
            judgement == Judgement.FlawlessP ? "Flawless +" :  judgement.ToString();


        /// <summary>
        /// Returns the text for the provided judgement.
        /// </summary>
        /// <returns></returns>
        public static Color GetJudgmentColor(Judgement judgement) =>
            judgement switch
            {
                Judgement.FlawlessP => Colors.MediumPurple.Lightened(0.1f),
                Judgement.Flawless => Colors.MediumPurple,
                Judgement.Clean => Colors.Blue,
                Judgement.Fair => Colors.GreenYellow,
                Judgement.Deficient => Colors.Orange,
                Judgement.Terrible => Colors.DarkOrange,
                Judgement.Miss => Colors.DarkRed,
                _ => throw new ArgumentOutOfRangeException(nameof(judgement), judgement, null)
            };
    }

    public enum Judgement
    {
        FlawlessP,
        Flawless,
        Clean,
        Fair,
        Deficient,
        Terrible,
        Miss
    }
}
