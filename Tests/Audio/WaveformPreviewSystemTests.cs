using System;
using System.IO;
using System.Text;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Audio;

[TestFixture]
public sealed class WaveformPreviewSystemTests
{
    [Test]
    public void LoadsLevelsAndFillsTheEditorDuration()
    {
        string path = WriteWav();
        try
        {
            var store = new EntityStore();
            Entity editor = store.CreateEntity(
                new LevelInfo("Wave", TestSongs.At(path)),
                new Playhead(0, 120, 0, false),
                new WaveformPreview(""));
            var root = new SystemRoot(store);
            root.Add(new WaveformPreviewSystem());

            root.Update(new UpdateTick(1f / 60f, 0));

            WaveformPreview preview = editor.GetComponent<WaveformPreview>();
            Assert.That(preview.Data, Is.Not.Null);
            Assert.That(preview.Data!.DurationMs, Is.EqualTo(100).Within(5));
            Assert.That(editor.GetComponent<Playhead>().DurationMs, Is.EqualTo(preview.Data.DurationMs));
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
        writer.Write(236);
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
        writer.Write(200);
        for (int i = 0; i < 100; i++)
            writer.Write((short)1_000);
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }
}
