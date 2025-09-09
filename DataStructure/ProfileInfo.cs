// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XanaduProject.DataStructure
{
    public class ProfileInfo
    {
        public string ProfileName { get; set; }

        public DateTime CreationDate { get; set; }

        public int Xp { get; set; }

        public string Name { get; set; }

        [JsonIgnore]
        public string ProfilePath { get; set; }

        [JsonIgnore]
        public List<Score> Scores { get; set; } = new();
    }
}
