// xanadu.play: the run being played, as values. Where the player is and what
// it is doing (`player.`), how the run is going (`score.`), the song
// (`song.`), Contact's bars (`contact.`), and when things last happened
// (`events.`), to time clips from like the play keys:
//
//     Clip flash = clock.since(events.hit).clip(0, 0.3).eased(eases.outQuad);
//     float near = 1 - saturate(length(player.position) / 400);
//     c.paint(Circle(40 + 20 * score.accuracy), colours.gold.withAlpha(near));
//
// In a level the game fills them; in the shader editor the lab's stand-ins
// do (the pointer is the player, play keys fake hits and misses).
module play;
import xanadu.stage;

/// Something that happens during play. The presets are under `events.`.
public struct Event
{
    /// Its number, as the game numbers it (see `events.`).
    public int id;

    public __init(int id) { this.id = id; }

    /// Seconds since it last happened; 1e6 when it never has.
    public property float since
    {
        get
        {
            if (id == 100)
                return min(playState.events[2].z, playState.events[2].w);
            if (id == 101)
                return min(playState.events[3].w, playState.events[4].x);
            return id >= 0 && id < 20 ? playState.events[id / 4][id % 4] : 1e6;
        }
    }

    /// Whether it happened within the last `seconds`.
    public bool within(float seconds) { return since < seconds; }
};

/// The play events, numbered as the game numbers them.
public namespace events
{
    /// Any note hit, whatever its judgement.
    public static const Event hit = Event(0);
    /// A note missed.
    public static const Event miss = Event(1);
    public static const Event flawlessPlus = Event(2);
    public static const Event flawless = Event(3);
    public static const Event clean = Event(4);
    public static const Event fair = Event(5);
    public static const Event deficient = Event(6);
    public static const Event terrible = Event(7);
    /// Any judgement at all, misses and failed barriers too: the last judgement.
    public static const Event judged = Event(8);
    /// The combo went back to 0.
    public static const Event comboBroken = Event(9);
    /// A dash note moved the player.
    public static const Event dash = Event(10);
    /// A momentum note pushed the player.
    public static const Event momentum = Event(11);
    /// The song started playing.
    public static const Event songStart = Event(12);
    /// The 3-2-1 countdown reached GO.
    public static const Event countdownDone = Event(13);
    /// The player died (a spike).
    public static const Event death = Event(14);
    /// A Contact barrier was paid from the bars.
    public static const Event barrierPaid = Event(15);
    /// A Contact barrier couldn't be paid.
    public static const Event barrierFailed = Event(16);
    /// Whichever of dash and momentum happened last.
    public static const Event moved = Event(100);
    /// Whichever of barrierPaid and barrierFailed happened last.
    public static const Event barrier = Event(101);
}

/// Judgements as the game numbers them, for `score.judgement`.
public namespace judgements
{
    public static const int none = 0;
    public static const int flawlessPlus = 1;
    public static const int flawless = 2;
    public static const int clean = 3;
    public static const int fair = 4;
    public static const int deficient = 5;
    public static const int terrible = 6;
    public static const int miss = 7;
    public static const int contact = 9;
}

/// The player character. Read it through `player.`.
public struct Player
{
    internal int unused;

    public __init() { unused = 0; }

    /// Its centre on the target, in pixels.
    public property float2 screen { get { return playState.player.xy; } }

    /// Its centre in the quad's pixels from its middle, like `canvas.centred` (and the lab's `mouse`).
    public property float2 position { get { return (shading.canvas.uvAt(screen) - 0.5) * shading.canvas.size; } }

    /// Its velocity in pixels a second, on the target.
    public property float2 velocity { get { return playState.player.zw; } }

    /// How fast it is going, in pixels a second.
    public property float speed { get { return length(velocity); } }

    /// Where it is on the playfield, 0..1 from the top-left.
    public property float2 onPlayfield { get { return (screen - playState.playfield.xy) / max(playState.playfield.zw, float2(1.0)); } }

    /// Standing on the floor or a block.
    public property bool grounded { get { return playState.state.x > 0.5; } }

    /// Off the ground.
    public property bool inAir { get { return !grounded; } }

    /// Hanging in a dash.
    public property bool dashing { get { return playState.state.y > 0.5; } }

    /// The ground mode is HOLD: sideways speed lasts only while the key is held.
    public property bool holding { get { return playState.state.z > 0.5; } }

    /// The ground mode is SKID: the ground keeps sideways speed.
    public property bool skidding { get { return !holding; } }

    /// Dead: a spike shattered it.
    public property bool dead { get { return playState.state.w > 0.5; } }

    /// The way the last dash or momentum note sent it, unit length (y down); 0 before any.
    public property float2 lastMove { get { return playState.moves.xy; } }
};

public static const Player player = Player();

/// How the run is going. Read it through `score.`.
public struct Score
{
    internal int unused;

    public __init() { unused = 0; }

    /// 0..1.
    public property float accuracy { get { return playState.score.x; } }

    public property int combo { get { return int(playState.score.y); } }

    public property int maxCombo { get { return int(playState.score.z); } }

    /// 0 to 1,000,000.
    public property float points { get { return playState.score.w; } }

    /// The last judgement: `judgements.flawless`, `judgements.miss`, ….
    public property int judgement { get { return int(playState.judgement.x); } }
};

public static const Score score = Score();

/// The song. Its beats are `clock.beats`. Read it through `song.`.
public struct Song
{
    internal int unused;

    public __init() { unused = 0; }

    /// How far through it is, 0..1.
    public property float progress { get { return playState.judgement.y; } }

    /// Seconds into it.
    public property float seconds { get { return playState.song.z; } }

    /// Its tempo now.
    public property float bpm { get { return playState.song.w; } }

    /// Beats since it started, from its timing (the same as `beat`).
    public property float beats { get { return playState.song.x; } }

    /// Seconds until the next note is due; 1e6 when none is left.
    public property float untilNote { get { return playState.song.y; } }
};

public static const Song song = Song();

/// Contact's three bars, one per action key. Read it through `contact.`.
public struct Contact
{
    internal int unused;

    public __init() { unused = 0; }

    /// The three bars, 0 to `cap` each.
    public property float3 bars { get { return playState.contact.xyz; } }

    /// The most a bar holds.
    public property float cap { get { return playState.contact.w; } }

    /// The three bars, 0..1 full.
    public property float3 fill { get { return bars / max(cap, 1.0); } }
};

public static const Contact contact = Contact();
