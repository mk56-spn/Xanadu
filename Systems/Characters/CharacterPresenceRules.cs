using System;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>
/// A character dims over the beat after its last hit, leaving only
/// its hands at full light, which flash white in turn on each beat, left then right,
/// so the music still has a body while it waits. Any hit brings it straight back.
/// </summary>
public static class CharacterPresenceRules
{
    /// <summary>
    /// The bones whose attachments are kept when the rest fades: the left hand
    /// pulses on even beats, the right on odd. Both hands share one art group,
    /// so they are told apart by bone.
    /// </summary>
    public const string LeftHand = "hand_l";

    public const string RightHand = "hand_r";

    /// <summary>How many beats after a hit the fade starts. Zero starts it on the beat that was hit.</summary>
    public const float WaitBeats = 0f;

    /// <summary>How many beats the fade takes once it starts: one, so it is dimmed by the next beat.</summary>
    public const float FadeBeats = 1f;

    /// <summary>
    /// How bright the body is once faded: 0 is black, 1 is as posed. It dims rather than
    /// turning see-through, so overlapping parts never show through each other.
    /// </summary>
    public const float FadedShade = 0.4f;

    /// <summary>How quickly a pulse dies away over its beat; higher is a sharper tap.</summary>
    public const float PulseDecay = 6f;

    /// <summary>
    /// How much a hand's colour is multiplied by at the top of its pulse. The
    /// art is a dark grey, so this takes it to white; between pulses it is as posed.
    /// </summary>
    public const float PulseLight = 4f;

    /// <summary>What a character shows this frame: the body's shade and each hand's light (1 is as posed).</summary>
    public readonly record struct Look(float BodyShade, float LeftLight, float RightLight)
    {
        public static readonly Look Present = new(1f, 1f, 1f);
    }

    /// <summary>Stamps a hit, if the player is a rigged character; called wherever a note is hit.</summary>
    public static void Hit(Friflo.Engine.ECS.Entity player, long playheadMs)
    {
        if (player.HasComponent<CharacterPresence>())
            player.GetComponent<CharacterPresence>().LastHitMs = playheadMs;
    }

    /// <summary>
    /// The look <paramref name="sinceHitMs"/> after the last hit, at
    /// <paramref name="songMs"/> into a song of <paramref name="bpm"/> whose
    /// first beat is at <paramref name="offsetMs"/>.
    /// </summary>
    public static Look At(long sinceHitMs, long songMs, int bpm, long offsetMs) =>
        bpm <= 0 ? Look.Present : At(sinceHitMs, songMs, Xanadu.Levels.TempoMap.Single(bpm, offsetMs));

    /// <summary>The same on the song's tempo map: the wait is counted in beats, so it follows BPM changes.</summary>
    public static Look At(long sinceHitMs, long songMs, Xanadu.Levels.TempoMap tempo)
    {
        if (sinceHitMs < 0)
            return Look.Present;

        double waited = tempo.BeatAt(songMs) - tempo.BeatAt(songMs - sinceHitMs);
        float fade = Math.Clamp((float)((waited - WaitBeats) / FadeBeats), 0f, 1f);
        if (fade <= 0f)
            return Look.Present;
        fade = XafEasing.Progress(XafEasePreset.CubicInOut, fade);

        double beats = tempo.BeatAt(songMs);
        double beat = Math.Floor(beats);
        float pulse = MathF.Exp(-(float)(beats - beat) * PulseDecay) * fade;
        float flash = 1f + (PulseLight - 1f) * pulse;
        bool left = ((long)beat & 1) == 0;
        return new Look(1f + (FadedShade - 1f) * fade, left ? flash : 1f, left ? 1f : flash);
    }

    /// <summary>The index of the attachment on a bone, or -1 when the rig has none.</summary>
    public static int AttachmentOn(XafPackage package, string bone)
    {
        for (int i = 0; i < package.Attachments.Count; i++)
        {
            if (package.Attachments[i].BoneId == bone)
                return i;
        }

        return -1;
    }
}
