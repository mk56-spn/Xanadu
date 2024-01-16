// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
        public TestSerializableStage (int noteCount, int textureElementCount, int textElementCount)
        {
            addTextures(textElementCount, textureElementCount, noteCount);
        }
        public TestSerializableStage ()
        {
            addTextures(RandRange(0, 5000), RandRange(0, 10000), RandRange(0, 500));
        }

        private void addTextures(int textElementCount, int textureElementCount, int noteCount)
        {
            List<Element> tempElements = new List<Element>();
            DynamicTextures = new Texture[10];

            for (int i = 0; i < textElementCount; i++)
            {

                tempElements.Add(new TextElement
                {
                    Colour = new Color(Randf(), Randf(), Randf()),
                    Text = "Testing",
                    TextSize = RandRange(5, 300),
                    Scale = new Vector2((float)RandRange(0.1, 3), (float)RandRange(0.1, 3)),
                    Skew = RandRange(0, 1),
                    Position = new Vector2(Randf(), Randf()) * 100000,
                    Group = RandRange(0, 900),
                    Rotation = RandRange(0, 1080),
                    Zindex = RandRange(1, 1000)
                });
            }

            for (int j = 0; j < noteCount; j++)
                tempElements.Add(new NoteElement
                {
                    TimingPoint = RandRange(1, 800) * (60 / 200f),
                    Colour = Colors.White,
                    Scale = Vector2.One,
                    Position = new Vector2(Randf(), Randf()) * 1000,
                    Group = RandRange(0, 900),
                    Rotation = RandRange(0, 1080),
                    Zindex = RandRange(1, 1000)
                });

            for (int j = 0; j < textureElementCount; j++)
            {
                tempElements.Add(new TextureElement
                {
                    Colour = new Color(Randf(), Randf(), Randf()),
                    Scale = new Vector2((float)RandRange(0.1, 3), (float)RandRange(0.1, 3)),
                    Skew = RandRange(0, 1),
                    Position = new Vector2(Randf(), Randf()) * 100000,
                    Group = RandRange(0, 900),
                    Rotation = RandRange(0, 1080),
                    Zindex = RandRange(1, 1000)
                });
            }

            float separation = 50;
            for (int i = 0; i < 1000; i++)
            {
                tempElements.Add(new PhysicsElement
                {
                    Extents = new Vector2(separation, separation),
                    Colour = Colors.White,
                    Scale = Vector2.One,
                    Position = new Vector2(50 * i, 50),
                    Group = RandRange(0, 900),
                    Zindex = RandRange(1, 1000)
                });
            }

            for (int i = 0; i < DynamicTextures.Length; i++)
            {
                if (i % 2 == 0)
                    DynamicTextures[i] = new GradientTexture2D
                    {
                        FillFrom = new Vector2(Randf(), Randf()),
                        Fill = (GradientTexture2D.FillEnum)RandRange(0, 2),
                        FillTo = new Vector2(Randf(), Randf()),
                        Gradient = new Gradient { Colors = [Colors.Azure, Colors.Bisque, Colors.White with { A = 0.4f } ] , Offsets = [ 0, 0.6f, 0.9f ] }
                    };

                else
                {
                    DynamicTextures[i] = new NoiseTexture2D
                    {
                        Height = 200,
                        Width = 200,
                        Noise = new FastNoiseLite
                        {
                            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
                        }
                    };
                }
            }

            Elements = tempElements.ToArray();
        }
    }
}
