namespace Xanadu.Core.Rendering;

/// <summary>
/// The clocks every shader has (xanadu.stage's <c>Frame</c>) that the host doesn't keep
/// itself: the music's beat, from whoever plays music (the game: its song's timing).
/// </summary>
public static class ShaderClock
{
    /// <summary>Music beats since the music started (<c>beat</c>, <c>clock.beats</c>).</summary>
    public static float Beat;
}
