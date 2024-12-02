// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.Audio;
using XanaduProject.Serialization.Elements;
using XanaduProject.Tools;
using static Godot.Mathf;

namespace XanaduProject.Composer
{
		public partial class Waveform(ComposerRenderMaster composer) : Panel
		{
			private const int rate = 44;
			private const float audio_rate = 44100 / 44f;
			private float spacing = 1f;
			private const int height = 50;
			private float offset;
			private int size = 1500;
			private Vector2[] points = null!;
			private TrackHandler trackHandler = composer.TrackHandler;

			private Font defaultFont = ThemeDB. FallbackFont;
			private int defaultFontSize = ThemeDB. FallbackFontSize;

			public override void _EnterTree()
			{
				trackHandler.OnSongCommence += () =>
				{
					computeWaveform();
					QueueRedraw();
				};

				SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

			}

			public override void _Process(double delta) =>
				QueueRedraw();

			public override void _Draw()
			{
				DrawSetTransform(new Vector2(-(float)(trackHandler.TrackPosition * spacing * (44100f / rate)), 0), 0, Vector2.One);

				int getIntPosition = (int)Max(0, 2 * (44100 / 44f) * trackHandler.TrackPosition -size);
				var getCurrentSegment = points[getIntPosition..(getIntPosition + 2 * size)];

				Color[] colors = new Color[getCurrentSegment.Length / 2];

				for (int i = 0; i < colors.Length; i++)
				{
					float fadeFactor = 1 - Abs(i - colors.Length / 2) / (float)colors.Length * 2;
					colors[i] =  XanaduColors.XanaduGreen with {A = fadeFactor };
				}

				// Draws the currently on screen segment of the waveform;
				DrawMultilineColors(getCurrentSegment,colors);

				double bpm = 60 / trackHandler.Bpm;
				float temp = (float)(audio_rate * bpm);

				double nearestMeasurePosition = Snapped(audio_rate * trackHandler.TrackPosition * spacing, temp);

				// Draws the beat measures
				for (int i = -10; i < 10; i++)
				{
					DrawLine(new Vector2((float)nearestMeasurePosition + i * spacing * temp, -30),
						new Vector2((float)nearestMeasurePosition + i * spacing * temp, 30),
						Colors.White, 2);
				}

				// Draws the timeline representation of the notes
				foreach (var variable in composer.NoteProcessor.Notes.Select(c => c.Element))
					DrawCircle(new Vector2(variable.TimingPoint * audio_rate * spacing, 0), 10,
						new Color("66fff2"));

				foreach (var note in composer.SelectedAreas.Select(c => c.renderElement.Element).OfType<NoteElement>())
					DrawArc(new Vector2(note.TimingPoint * audio_rate * spacing, 0), 15, 0, 360, 30,
						new Color("ff66b8"));

				DrawSetTransform(Vector2.Zero, 0, Vector2.One);
				DrawLine(new Vector2(0, -40), new Vector2(0, 40), XanaduColors.XanaduPink, 4);
			}


			private void computeWaveform()
			{

				if (trackHandler.Buffer.Length == 0) return;

				Vector2[] buffer = trackHandler.Buffer;
				points = new Vector2[buffer.Length * 2];

				for (int i = 0; i < buffer.Length;  i ++)
				{
					points[i * 2] = new Vector2(i * spacing , Abs(buffer[i].X  * height));
					points[i * 2 + 1] =  new Vector2(i * spacing, -Abs(buffer[i].Y) * height);
				}
			}
		}
}
