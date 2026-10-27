using System;
using System.IO;
using NUnit.Framework;
using Xanadu.Replays;
using Xanadu.Settings;

namespace Xanadu.Tests.Replays;

[TestFixture]
public sealed class ReplayFileTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "replays-" + Path.GetRandomFileName());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void RoundTripKeepsEveryField()
    {
        Replay replay = Sample();

        Replay read = RoundTrip(replay);

        Assert.That(read.LevelId, Is.EqualTo(replay.LevelId));
        Assert.That(read.LevelName, Is.EqualTo("Mortal Coil — Character Test"));
        Assert.That(read.SongId, Is.EqualTo(replay.SongId));
        Assert.That(read.Player, Is.EqualTo("dhs"));
        Assert.That(read.RecordedAt, Is.EqualTo(replay.RecordedAt));
        Assert.That(read.NoteCount, Is.EqualTo(120));
        Assert.That(read.Result, Is.EqualTo(replay.Result));
        Assert.That(read.Events, Is.EqualTo(replay.Events));
    }

    [Test]
    public void LargeAndRepeatedTimesSurviveTheVarintDeltas()
    {
        Replay replay = Sample() with
        {
            Events =
            [
                new(0, KeybindAction.L1, true),
                new(0, KeybindAction.R3, true),
                new(127, KeybindAction.L1, false),
                new(128, KeybindAction.R3, false),
                new(16_384, KeybindAction.M, true),
                new(3_600_000, KeybindAction.M, false),
            ],
        };

        Assert.That(RoundTrip(replay).Events, Is.EqualTo(replay.Events));
    }

    [Test]
    public void EmptyReplayRoundTrips() =>
        Assert.That(RoundTrip(Sample() with { Events = [] }).Events, Is.Empty);

    [Test]
    public void WrongMagicIsRejected()
    {
        var stream = new MemoryStream("NOPE-not-a-replay"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => ReplayFile.Read(stream));
    }

    [Test]
    public void UnknownVersionIsRejected()
    {
        byte[] bytes = Bytes(Sample());
        bytes[3] = 99;

        Assert.Throws<InvalidDataException>(() => ReplayFile.Read(new MemoryStream(bytes)));
    }

    [Test]
    public void TruncatedFileIsRejectedNotCrashed()
    {
        byte[] bytes = Bytes(Sample());

        for (int length = 0; length < bytes.Length; length += 7)
        {
            Assert.That(
                () => ReplayFile.Read(new MemoryStream(bytes, 0, length)),
                Throws.TypeOf<InvalidDataException>(),
                $"length {length}");
        }
    }

    [Test]
    public void StoreSavesUniquelyNamedXrsFilesNewestFirst()
    {
        var store = new ReplayStore(_directory);
        Replay replay = Sample();

        string first = store.Save(replay);
        string second = store.Save(replay);

        Assert.That(first, Does.EndWith(".xrs"));
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(store.List(), Is.EquivalentTo(new[] { first, second }));
        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        Assert.That(ReplayStore.Load(first).Events, Is.EqualTo(replay.Events));
    }

    [Test]
    public void StoreListIsEmptyBeforeAnythingIsSaved() =>
        Assert.That(new ReplayStore(_directory).List(), Is.Empty);

    private static Replay RoundTrip(Replay replay) =>
        ReplayFile.Read(new MemoryStream(Bytes(replay)));

    private static byte[] Bytes(Replay replay)
    {
        var stream = new MemoryStream();
        ReplayFile.Write(stream, replay);
        return stream.ToArray();
    }

    private static Replay Sample() => new(
        "5c3478c5261946bd9eb2a79713adad17",
        "Mortal Coil — Character Test",
        "cfd622f1f39b48cf8d13c84b10f44694",
        "dhs",
        DateTimeOffset.FromUnixTimeMilliseconds(1_790_000_000_123),
        120,
        new ReplayResult(987_654, 0.9731, 88, 117, 120, 60, 30, 20, 5, 1, 1, 3),
        [
            new(1211, KeybindAction.L1, true),
            new(1290, KeybindAction.L1, false),
            new(1511, KeybindAction.R2, true),
            new(1600, KeybindAction.R2, false),
        ]);
}
