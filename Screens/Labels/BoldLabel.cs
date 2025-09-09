// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.Labels
{
    public partial class BoldLabel : Label
    {
        private readonly LabelSettings labelSettings = new()
        {
            FontSize = 60,
            Font = FontSource.SURF,
        };
        public BoldLabel()
        {
            LabelSettings = labelSettings;
        }
    }
}
