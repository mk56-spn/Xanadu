// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens.Player
{
    public partial class ScoreProcessor : Node
    {
        public event Action<int>? OnComboChanged;

        private int combo;

        /// <summary>
        /// The current combo.
        /// </summary>
        public int Combo
        {
            get => combo;
            set
            {
                if (value.Equals(combo)) return;

                OnComboChanged?.Invoke(value);
                combo = value;
            }
        }

        public ScoreProcessor (Stage stage)
        {
            foreach (var note in stage.GetNotes())
            {
                note.OnNoteJudged += judgement =>
                {
                    if (judgement == Judgement.Miss)
                    {
                        Combo = 0;
                        return;
                    }
                    Combo++;
                };
            }
        }
    }
}
