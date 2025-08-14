// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

// Abstract base class for text buttons

using Godot;

namespace XanaduProject.Buttons
{
    public abstract partial class TextButton : BaseButton
    {
        protected StringName Text;
        protected Font Font = ThemeDB.FallbackFont;
        protected Vector2 StringSize;

        private int fontSize;
        public int FontSize
        {
            get => fontSize;
            set
            {
                fontSize = value;
                StringSize = Font.GetStringSize(Text, fontSize: fontSize);
                CustomMinimumSize = StringSize * new Vector2(1.2f, 1.5f);
            }
        }

        public TextButton(string text)
        {
            this.Text = text;
            FontSize = 40;
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawString(Font, new Vector2(2, StringSize.Y), Text, fontSize: FontSize);
        }
    }
}
