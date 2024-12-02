// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
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
            addTextures(textElementCount, textureElementCount);
        }
        public TestSerializableStage ()
        {
            addTextures(RandRange(0, 5000), RandRange(0, 10000));
        }

        private void addTextures(int textElementCount, int textureElementCount)
        {
            List<Element> tempElements = new List<Element>();
            DynamicTextures = new Texture[10];

            for (int i = 0; i < textElementCount; i++)
            {

                tempElements.Add(new TextElement
                {
                    Colour = new Color(Randf(), Randf(), Randf()),
                    Text = "Testing",
                    TextSize = 20,
                    Scale = Vector2.One,
                    Skew = 0,
                    Position = new Vector2(Randf(), Randf()) * 100000,
                    Group = RandRange(0, 900),
                    Rotation = 0,
                    Zindex = RandRange(1, 1000)
                });
            }

            tempElements.AddRange(notes.Select(t => new NoteElement
            {
                Colour = Colors.White,
                TimingPoint = t,
                Position = new Vector2(Randf(), -Randf()) * 1000,
                Scale = Vector2.One,
                Group = RandRange(0, 900),
                Rotation = RandRange(0, 1080),
                Zindex = RandRange(1, 1000)
            }));

            for (int j = 0; j < textureElementCount; j++)
            {
                tempElements.Add(new TextureElement
                {
                    Colour = new Color(Randf(), Randf(), Randf()),
                    Scale = new Vector2((float)RandRange(0.1, 3), (float)RandRange(0.1, 3)),
                    Skew = RandRange(0, 1),
                    Position = new Vector2(Randf(), -Randf()) * 100000,
                    Group = RandRange(0, 900),
                    Rotation = RandRange(0, 1080),
                    Zindex = RandRange(1, 1000)
                });
            }

            for (int i = 0; i < 100; i++)
            {
                tempElements.Add(new PhysicsElement
                {
                    Colour = Colors.White,
                    Scale = Vector2.One,
                    Position = new Vector2(200 + 32 * i, float.Sin(i) * 16 -8 * i),
                    Group = RandRange(0, 900),
                    Zindex = RandRange(1, 1000)
                });
            }

            for (int i = 0; i < DynamicTextures.Length; i++)
            {
                if (i % 2 == 0)
                    DynamicTextures[i] = new GradientTexture2D
                    {
                        FillFrom = new Vector2(0.5f, 0.5f),
                        Fill = (GradientTexture2D.FillEnum)RandRange(0, 2),
                        FillTo = new Vector2(1.0f, 0.5f),
                        Gradient = new Gradient
                        {
                            Colors = [Colors.Transparent, Colors.White, Colors.Transparent] ,
                            Offsets = [ 0.85f, 0.95f, 1.0f ]
                        }
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

        private readonly float[] notes =
        [
            0.6f,
            1.2f,
            1.8f,
            2.4f,
            6f,
            9.3f,
            12f
       ];
    }
}
