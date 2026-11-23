using System.Runtime.InteropServices;

namespace Xanadu.Core.Rendering.Rhythm;

/// <summary>
/// The game's shader globals (<c>rhythm.play</c>'s <c>playFrame</c> and <c>chart</c>),
/// declared through the shader API like any user would: the API itself knows
/// nothing about rhythm. The game declares them at start-up; the shader editor
/// declares them too, filled by its stand-ins. A host that doesn't (the UI
/// showcase) can't compile an effect that reads them.
/// </summary>
public static class RhythmGlobals
{
    /// <summary><c>rhythm.play</c>'s <c>PlayFrame</c>, as <see cref="ShaderPlay"/> and <see cref="ShaderKeys"/> fill it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlayFrame
    {
        public Vector4 Player, Playfield, State, Moves, Score, Judgement, Contact, Song;
        public Vector4 Events0, Events1, Events2, Events3, Events4;
        public Vector4 Chart, DirectionKeys, ActionKeys;

        public const int Size = 16 * 16;

        public static PlayFrame Now() => new()
        {
            Player = new Vector4(ShaderPlay.Position.X, ShaderPlay.Position.Y, ShaderPlay.Velocity.X, ShaderPlay.Velocity.Y),
            Playfield = new Vector4(ShaderPlay.PlayfieldCorner.X, ShaderPlay.PlayfieldCorner.Y, ShaderPlay.PlayfieldSize.X, ShaderPlay.PlayfieldSize.Y),
            State = new Vector4(Flag(ShaderPlay.Grounded), Flag(ShaderPlay.Dashing), Flag(ShaderPlay.GroundHold), Flag(ShaderPlay.Dead)),
            Moves = new Vector4(ShaderPlay.LastMove.X, ShaderPlay.LastMove.Y, 0, 0),
            Score = new Vector4(ShaderPlay.Accuracy, ShaderPlay.Combo, ShaderPlay.MaxCombo, ShaderPlay.Score),
            Judgement = new Vector4(ShaderPlay.LastJudgement, ShaderPlay.SongProgress, 0, 0),
            Contact = new Vector4(ShaderPlay.ContactBars.X, ShaderPlay.ContactBars.Y, ShaderPlay.ContactBars.Z, ShaderPlay.ContactCap),
            Song = new Vector4(ShaderPlay.Beat, ShaderPlay.UntilNote, ShaderPlay.SongSeconds, ShaderPlay.Bpm),
            Events0 = Events(0), Events1 = Events(4), Events2 = Events(8), Events3 = Events(12), Events4 = Events(16),
            Chart = new Vector4(ShaderChart.Count, ShaderPlay.PixelsPerUnit, 0, 0),
            DirectionKeys = new Vector4(ShaderKeys.Since(0), ShaderKeys.Since(1), ShaderKeys.Since(2), ShaderKeys.Since(3)),
            ActionKeys = new Vector4(ShaderKeys.Since(4), ShaderKeys.Since(5), ShaderKeys.Since(6), ShaderKeys.Never),
        };

        private static float Flag(bool on) => on ? 1 : 0;

        private static Vector4 Events(int first) => new(
            ShaderPlay.SinceSlot(first), ShaderPlay.SinceSlot(first + 1), ShaderPlay.SinceSlot(first + 2), ShaderPlay.SinceSlot(first + 3));
    }

    private static int _chartWritten = -1;

    /// <summary>Declares both globals; their writers then run once a frame. Declaring again is harmless.</summary>
    public static void Declare()
    {
        ShaderGlobals.Declare("playFrame", PlayFrame.Size, global =>
        {
            global.Write(PlayFrame.Now());
            // The music clock every shader has (xanadu.stage's `beat`) is the song's.
            ShaderClock.Beat = ShaderPlay.Beat;
        });
        ShaderGlobals.Declare("chart", ChartNote.Size, global =>
        {
            if (_chartWritten == ShaderChart.Version)
                return;
            _chartWritten = ShaderChart.Version;
            global.Write(ShaderChart.Notes);
        });
    }
}
