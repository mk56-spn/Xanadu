namespace Xanadu.Tools.Moves;

/// <summary>Whether a card's rule is in the game, decided but not built, or still undecided.</summary>
internal enum MoveStatus
{
    Built,
    Planned,
    Open,
}

/// <summary>One square: what happens (<paramref name="Event"/>) in its sector, and the edge cases.</summary>
internal sealed record MoveCard(string Event, string What, string? Edge = null, MoveStatus Status = MoveStatus.Built);

/// <summary>Where the character can be, and every card that applies there.</summary>
internal sealed record MoveSector(string Name, string Blurb, MoveCard[] Cards);

/// <summary>
/// The character's move system as a chart, written by hand.
/// UPDATE THIS whenever movement changes: PlayerMovementSystem, DashHangSystem,
/// GroundModeSystem, GroundStepSystem, FrictionBarSystem, PlayerArenaSystem,
/// PlayerSpeedCapSystem, PlayNoteHits, HitGhostSystem, SlopeSlideSystem,
/// WallSlideSystem, PlayScene's start, or the Stationary character section of
/// Docs/design.md.
/// </summary>
internal static class MovesChart
{
    public static readonly MoveSector[] Sectors =
    [
        new("GROUND · SKID", "Floor or a block top. The default mode (F8 switches).",
        [
            new("START", "Hidden through the countdown. 0.4 s before the first note she fades in above it, falling at 4 m/s, placed so gravity lands her on it at its time. The camera holds on that note until then.",
                "A start point fades her in where it says, from a standstill; with no notes she drops from the top of the field when the song starts."),
            new("DASH", "Teleports 250 / 375 / 500 (single / double / triple) the pressed way, 45° clockwise on a rhombus. Measured from the note's x and her standing height. Sideways speed kept, vertical cleared, hang starts.",
                "Height is clamped to where her centre can be. Ends a ground step."),
            new("MOMENTUM", "Sets velocity to heading × the note's speed, on both axes. Replaces, never stacks.",
                "Turns friction off, ends a hang and a ground step."),
            new("ACTION NOTE", "Turns to face the note and kicks (high, mid or low by the note's height). Her ghost launches at the note and a little past it; she stays. Steps 80 towards the note's side over 70 ms, easing to a stop.",
                "Notes at one time share a step: same side adds nothing, both sides cancel. No step while rising."),
            new("NOTHING", "No friction: she keeps her sideways speed until a friction bar.",
                "Crossing a bar by moving (not by dashing past it) turns friction on, and she slows to a stop."),
            new("EDGES", "Leaving the field sideways wraps to the other side. Floor and ceiling stop only motion into them, so a launch can leave."),
        ]),
        new("GROUND · HOLD", "F8. Same as SKID except letting go.",
        [
            new("HOLD", "Keeps sideways speed only while the key of the last directional note she hit is held.",
                "Not checked during a dash hang."),
            new("LET GO", "Sideways speed drops to zero at once."),
            new("LET GO, LATER?", "Stop at once or slow down? Is the hold judged?", Status: MoveStatus.Open),
        ]),
        new("AIR", "Falling or rising, not hanging.",
        [
            new("DASH", "As on the ground, but measured from the note on both axes. A late hit is shifted by her sideways speed × how late.",
                "What follows runs from the note's time, not the press."),
            new("MOMENTUM", "As on the ground: sets velocity, ends a hang."),
            new("ACTION NOTE", "Faces the note and kicks; her ghost launches at the note. She doesn't move."),
            new("NOTHING", "Normal gravity. Sideways and falling speed are capped at the strongest momentum note; rising is not."),
            new("LANDING", "SKID keeps the sideways speed she landed with. Lands feet first on blocks (the body tucks in the air)."),
            new("ACTION MOVE?", "Trying the ghost launch for now. Should action notes move her for real later?", Status: MoveStatus.Open),
        ]),
        new("DASH HANG", "After a dash: gravity off a quarter beat (50–175 ms), then eases back over the same.",
        [
            new("DASH", "Restarts the hang from the new dash note's time."),
            new("MOMENTUM", "Ends the hang at once; gravity full."),
            new("ACTION NOTE", "The hang carries on; only the ghost launches."),
            new("WALL NOTE", "Sticking to a wall ends the hang."),
        ]),
        new("SLOPE", "A block made a ramp (L in the editor): the half of its box under the diagonal.",
        [
            new("ON IT", "Snapped to 600 units/s down the ramp, whatever she landed with. One speed for every slope.",
                "Moving away from the surface (a launch) isn't sliding. Not ground: no step, no HOLD, dashes measure from the note."),
            new("OFF THE BOTTOM", "Keeps the slide's velocity; SKID ground keeps its sideways part."),
            new("DASH / MOMENTUM", "As in the air; the next frame on the ramp snaps her back to the slide."),
            new("BY ANGLE?", "One speed for every slope, or should it follow the angle?", Status: MoveStatus.Open),
        ]),
        new("WALL", "A wall note: a flat slab set on a wall's face, its hitbox sticking out 14 units each way.",
        [
            new("TOUCH", "Touched within the miss window of its beat: she sticks to the wall on the note's side, wall particles burst. No press, not scored.",
                "Ends a hang and a ground step. Untouched, it leaves once its window passes."),
            new("SLIDING", "Held flush to the face, sliding down at 260 units/s, one speed for every wall, trickling particles."),
            new("ENDS", "On landing, when no block face is beside her any more, or on a dash or momentum note."),
        ]),
    ];
}
