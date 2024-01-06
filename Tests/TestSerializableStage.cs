// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.GD;

namespace XanaduProject.Tests
{
    /// <summary>
    /// Creates a randomised instance of a <see cref="SerializableStage"/>
    /// </summary>
    public class TestSerializableStage : SerializableStage
    {
        public TestSerializableStage ()
        {
            Elements = new Element[100];
            DynamicTextures = new Texture[10];

            for (int i = 0; i < Elements.Length; i++)
            {
                if (i % 2 == 0)
                    Elements[i] = new Element
                    {
                        Skew = RandRange(0, 1),
                        Position = new Vector2(Randf(), Randf()) * 1000,
                        Group = RandRange(0, 900),
                        Rotation = 400,
                    };
                else
                {
                    Elements[i] = new TextElement
                    {
                        Text = "Testing",
                        Position = new Vector2(Randf(), Randf()) * 1000,
                        Group = RandRange(0, 900),
                        Rotation = 0,
                    };
                }
            }

            for (int i = 0; i < DynamicTextures.Length; i++)
            {
                DynamicTextures[i] = new GradientTexture2D
                {
                    FillFrom = new Vector2(Randf(), Randf()),
                    Fill = (GradientTexture2D.FillEnum)RandRange(0, 2),
                    FillTo = new Vector2(Randf(), Randf()),
                    Gradient = new Gradient { Colors = [Colors.Azure, Colors.Bisque ] , Offsets = [ 0, 0.6f, 0.9f ] }
                };
            }
        }
    }
}
