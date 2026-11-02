using NUnit.Framework;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

public sealed class AnimatorSvgWatchTests : AnimatorToolTest
{
    private const string Body = "res://body.svg";
    private FakeAnimatorSvgCatalog _catalog = null!;

    private int Version => Session.GetComponent<AnimatorDocument>().Version;

    private string Status => Session.GetComponent<AnimatorStatus>().Message;

    protected override void AddSystems()
    {
        _catalog = new FakeAnimatorSvgCatalog();
        _catalog.Add(Body, "body");
        _catalog.Stamps[Body] = 100L;
        Root.Add(new AnimatorSvgWatchSystem(_catalog));
    }

    private void Poll() => Frame(1, AnimatorSvgWatchSystem.PollSeconds);

    [Test]
    public void OpenFileAsksTheSystemToOpenTheSelectedSvgsFile()
    {
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.OpenArtFile));
        Assert.That(Files.OpenedPath, Is.Null, "nothing selected");
        Assert.That(Status, Is.EqualTo("SELECT AN SVG FIRST"));

        Session.GetComponent<AnimatorSelection>().Attachment = 0;
        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.OpenArtFile));
        Assert.That(Files.OpenedPath, Is.EqualTo(Session.GetComponent<AnimatorDocument>().Package.Attachments[0].SvgPath));
    }

    [Test]
    public void ASavedFileReloadsOnceItsWriteHasSettled()
    {
        Poll();
        int version = Version;

        _catalog.Stamps[Body] = 200L;
        Poll();
        Assert.That(Version, Is.EqualTo(version), "may still be mid-write");

        Poll();
        Assert.That(Version, Is.EqualTo(version + 1));
        Assert.That(Status, Is.EqualTo("RELOADED body"));

        Poll();
        Assert.That(Version, Is.EqualTo(version + 1), "only once per save");
    }

    [Test]
    public void AnUntouchedFileNeverReloads()
    {
        Poll();
        int version = Version;
        Poll();
        Poll();
        Assert.That(Version, Is.EqualTo(version));
    }

    [Test]
    public void AFileThatKeepsChangingWaitsUntilItStops()
    {
        Poll();
        int version = Version;
        _catalog.Stamps[Body] = 200L;
        Poll();
        _catalog.Stamps[Body] = 300L;
        Poll();
        Assert.That(Version, Is.EqualTo(version));
        Poll();
        Assert.That(Version, Is.EqualTo(version + 1));
    }

    [Test]
    public void AFileBrieflyGoneDuringASaveIsNotAChange()
    {
        Poll();
        int version = Version;
        _catalog.Stamps.Remove(Body);
        Poll();
        _catalog.Stamps[Body] = 100L;
        Poll();
        Poll();
        Assert.That(Version, Is.EqualTo(version));
    }

    [Test]
    public void ASaveThatLostTheGroupSaysSo()
    {
        Poll();
        _catalog.Add(Body, "layer1");
        _catalog.Stamps[Body] = 200L;
        Poll();
        Poll();
        Assert.That(Status, Is.EqualTo("NO GROUP ID \"body\""));
    }

    [Test]
    public void ReloadingKeepsThePlayheadAndClip()
    {
        Runtime.Seek(0.5f);
        Runtime.SetPaused(true);
        Poll();
        _catalog.Stamps[Body] = 200L;
        Poll();
        Poll();
        Assert.That(Runtime.CurrentTime, Is.EqualTo(0.5f));
        Assert.That(Runtime.CurrentClip, Is.EqualTo("idle"));
    }
}
