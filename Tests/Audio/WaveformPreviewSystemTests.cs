using System;
using System.IO;
using System.Text;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Editor;

namespace Writer.Tests.Audio;

[TestFixture]
public sealed class WaveformPreviewSystemTests
{
    [Test]
    public void LoadsPeaksAndFillsTheEditorDuration()
    {
        string path = WriteWav();
        try
        {
            var store = new EntityStore();
            Entity editor = store.CreateEntity(
                new LevelInfo("Wave", path),
                new Playhead(0, 120, 0, false),
                new WaveformPreview("", Array.Empty<float>(), 0));
            var root = new SystemRoot(store);
            root.Add(new WaveformPreviewSystem());

            root.Update(new UpdateTick(1f / 60f, 0));

            WaveformPreview preview = editor.GetComponent<WaveformPreview>();
            Assert.That(preview.Peaks, Is.Not.Empty);
            Assert.That(preview.DurationMs, Is.EqualTo(2));
            Assert.That(editor.GetComponent<Playhead>().DurationMs, Is.EqualTo(2));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteWav()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "writer-preview-" + Guid.NewGuid().ToString("N") + ".wav");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(40);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(1_000);
        writer.Write(2_000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(4);
        writer.Write((short)1_000);
        writer.Write((short)1_000);
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }
}
