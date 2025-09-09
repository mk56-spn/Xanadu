// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace XanaduProject.DataStructure
{
    [Serializable]
    public class Score
    {
        public int SongIndex { get; set; }
        public string StagePath { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<Judgement, int> Judgements { get; set; }
        public int MaxCombo { get; set; }
        public float Accuracy { get; set; }
        public float UnstableRate { get; set; }
    }
}
