using Xanadu.Audio;

namespace Xanadu.Logic.Audio;

/// <summary>The feedback sounds: the menus' under <c>Assets/Audio/Ui</c>, a note hit's under <c>Assets/Audio/Gameplay</c>.</summary>
public enum FeedbackSound
{
    Move,
    Select,
    Back,
    Error,
    SongSwitch,
    Hit,
}

/// <summary>The game's <see cref="FeedbackSound"/>s on one <see cref="SoundBank"/>.</summary>
public sealed class FeedbackSoundPlayer : System.IDisposable
{
    private static readonly string[] Files =
    [
        "Assets/Audio/Ui/menu-cursor-move.flac",
        "Assets/Audio/Ui/menu-select.flac",
        "Assets/Audio/Ui/menu-back.flac",
        "Assets/Audio/Ui/menu-error.flac",
        "Assets/Audio/Ui/song-switch.flac",
        "Assets/Audio/Gameplay/hit.flac",
    ];

    private readonly SoundBank _bank = new(Files);

    public void Play(FeedbackSound sound) => _bank.Play((int)sound);

    public void Dispose() => _bank.Dispose();
}
