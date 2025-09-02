// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace XanaduProject.DataStructure
{
    public class StageInfo(string stagePath, string metadata, string stageName, string[] creatorList)
    {
        [JsonIgnore]
        public string StagePath { get; set; } = stagePath;

        [JsonIgnore]
        public string Metadata { get; set; } = metadata;

        [JsonInclude]
        public int SongIndex = 1;
        [JsonInclude]
        public string StageName = stageName;
        [JsonInclude]
        public int Difficulty;
        [JsonInclude]
        public string[] CreatorList = creatorList;
    }
}
