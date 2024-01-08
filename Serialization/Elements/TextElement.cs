// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Serialization.Elements
{
    public record TextElement : Element
    {
        public required string Text;
        public int TextSize = 30;

        public override Vector2 GetSize() =>
            ThemeDB.FallbackFont.GetStringSize(Text, fontSize: TextSize);
    }
}
