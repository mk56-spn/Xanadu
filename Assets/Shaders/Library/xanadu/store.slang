// xanadu.store: what a graph keeps, by plain names.
//
//     [Size(256)] List<Part> parts;          // 256 parts, kept from frame to frame
//     Counter drawn;                         // one uint to count with
//     Image<float4> shown;                   // the item's box, colour
//     [Clear(1e30)] Image<float> depth;      // one float a pixel, starting far away
//
// A list is read and written like an array (parts[i]); a library pass or schedule takes one as
// `List<uint> keys`. Many threads can add to one place at once with add, which hands back what
// it held before: the slot a thread fills when lists grow, or a tally when they don't.
//
//     uint slot = drawn.add(1);              // the counter's one number
//     uint top = lists.add(ALIVE, 1);        // the number at lists[ALIVE]
//
// One value the graph keeps, a struct of its own, is Stored: read and written by its plain name,
// from any pass or draw, zero when made. Its uint fields take InterlockedAdd and friends.
//
//     struct Book { uint written; uint frame; float owed[4]; };
//     Stored<Book> book;
//     book.frame += 1;                       // from one thread
//     InterlockedAdd(book.written, 1, slot); // from many
//
// A list's last frame is its [History]: the two swap once a frame, before the graph's first node,
// so the list starts each frame with what it held two frames ago and the history with last frame's.
//
//     [Size(CELLS)] List<float> field;       // this frame's, written whole
//     [History(field)] List<float> fieldBefore;  // last frame's, read
//
// Two more keep a list and its bookkeeping under one name, so no header slots are numbered by hand:
//
//     [Size(CAP)] Slots free;                // the free places in a pool of CAP, all free at first
//     [Size(CAP)] Gathered<uint> alive;      // a list threads add to, and how many it holds
//
//     uint slot;
//     if (free.take(slot)) pool[slot] = born; // a place, if one is free
//     free.give(i);                          // a place handed back
//     alive.add(i);                          // onto the end, safely from many threads
//     [Draw(..., copies: alive)]             // drawn once for each it holds
//     alive.clear();                         // from one thread, before the pass that adds
//
// Take and give in different passes: a place given back is ready to take from the next pass on.
//
// An image is read and written at a pixel (shown[group.pixel]); its format comes from what it
// holds, so a float is precise and a float4 is a half-float colour. [Precise] and [Compact]
// before an image change that (Slang/Texels.cs has the table).
module store;

/// A list of T the graph keeps on the GPU: [Size(n)] List<T> name;.
public typealias List<T> = RWStructuredBuffer<T>;

/// One T the graph keeps on the GPU, used by its plain name: Stored<T> name;.
public typealias Stored<T> = RWStructuredBuffer<T>;

/// One uint many threads count with at once: Counter name;.
public typealias Counter = RWStructuredBuffer<uint>;

/// A picture the graph keeps, a T at each pixel: Image<float4> name;.
public typealias Image<T : ITexelElement> = RWTexture2D<T>;

public extension RWStructuredBuffer<uint>
{
    /// Adds by to the number at `at`, safely when many threads add at once; what it held before.
    public uint add(uint at, uint by)
    {
        uint before;
        InterlockedAdd(this[at], by, before);
        return before;
    }

    /// Adds by to the first number (a Counter's); what it held before.
    public uint add(uint by) { return add(0, by); }

    /// Takes by from the number at `at`, safely when many threads take at once; what it held before.
    public uint take(uint at, uint by) { return add(at, 0u - by); }

    /// Keeps the larger of the number at `at` and value; what it held before.
    public uint keepLarger(uint at, uint value)
    {
        uint before;
        InterlockedMax(this[at], value, before);
        return before;
    }

    /// Keeps the smaller of the number at `at` and value; what it held before.
    public uint keepSmaller(uint at, uint value)
    {
        uint before;
        InterlockedMin(this[at], value, before);
        return before;
    }

    /// The first number: a Counter's count.
    public property uint count { get { return this[0]; } }
}

/// A list many threads add to at once, and how many it holds: [Size(n)] Gathered<T> name;.
/// Adding past n is turned away. Clear it from one thread before the pass that adds. The build
/// lays it out: its count (and a uint list's items) share the graph's bookkeeping list.
public struct Gathered<T>
{
    RWStructuredBuffer<uint> tally;
    uint at;
    RWStructuredBuffer<T> items;
    uint from;
    uint most;

    public __init(RWStructuredBuffer<uint> tally, uint at, RWStructuredBuffer<T> items, uint from, uint most)
    {
        this.tally = tally;
        this.at = at;
        this.items = items;
        this.from = from;
        this.most = most;
    }

    /// The most it holds.
    public property uint capacity { get { return most; } }

    /// How many it holds.
    public property uint count { get { return min(tally[at], most); } }

    /// Puts value on the end; false when it was full.
    public bool add(T value)
    {
        uint place;
        return add(value, place);
    }

    /// Puts value on the end, at `place`; false when it was full.
    public bool add(T value, out uint place)
    {
        place = tally.add(at, 1);
        if (place >= most)
        {
            tally.take(at, 1);
            return false;
        }
        items[from + place] = value;
        return true;
    }

    /// Empties it.
    public void clear() { tally[at] = 0; }

    public __subscript(uint i) -> T
    {
        get { return items[from + i]; }
        set { items[from + i] = newValue; }
    }
};

/// The free places in a pool of n, taken and given back by many threads at once: [Size(n)] Slots name;.
/// All n are free at first, in order from 0. The build lays it out in the graph's bookkeeping list.
public struct Slots
{
    // book[at]: places given back, stacked from book[at + 2]; book[at + 1]: places handed out fresh, from 0 up.
    RWStructuredBuffer<uint> book;
    uint at;
    uint most;

    public __init(RWStructuredBuffer<uint> book, uint at, uint most) { this.book = book; this.at = at; this.most = most; }

    /// The places in the pool.
    public property uint capacity { get { return most; } }

    /// How many are free.
    public property uint count { get { return book[at] + most - min(book[at + 1], most); } }

    /// A free place into slot; false (slot 0) when none is.
    public bool take(out uint slot)
    {
        uint back = book.take(at, 1);
        if (int(back) > 0)
        {
            slot = book[at + 2 + back - 1];
            return true;
        }
        book.add(at, 1);
        slot = book.add(at + 1, 1);
        if (slot < most)
            return true;
        book.take(at + 1, 1);
        slot = 0;
        return false;
    }

    /// Hands place i back.
    public void give(uint i) { book[at + 2 + book.add(at, 1)] = i; }

    /// Every place free again.
    public void reset()
    {
        book[at] = 0;
        book[at + 1] = 0;
    }
};

/// How many events a frame wrote and how many last frame's holds: an Events' own count.
public struct EventTally
{
    public uint written;    // this frame's, counted as they're written (may pass the room)
    public uint heard;      // last frame's that were kept
};

/// Things that happen in one frame, read the next: [Size(n)] Events<T> name;. Many threads add
/// this frame's; every thread can read last frame's. Turn it once a frame, from one thread,
/// before anything adds (in the pass that plans the frame): this frame's become last frame's.
///
///     deaths.turn();                         // one thread, first
///     deaths.add(d);                         // many threads; false when the frame's room is full
///     for (uint k = 0; k < deaths.heard; k++) Death d = deaths[k];   // last frame's
[Block(n)]
public struct Events<T>
{
    public uint n;
    [Size(n)] List<T> items;                  // this frame's
    [History(items)] List<T> last;            // last frame's
    Stored<EventTally> tally;

    /// The most a frame keeps.
    public property uint capacity { get { return n; } }

    /// How many of last frame's there are to read.
    public property uint heard { get { return tally.heard; } }

    /// Adds e to this frame's; false when the frame's room is full.
    public bool add(T e)
    {
        uint slot;
        InterlockedAdd(tally.written, 1, slot);
        if (slot >= n)
            return false;
        items[slot] = e;
        return true;
    }

    /// Last frame's i-th.
    public __subscript(uint i) -> T { get { return last[i]; } }

    /// From one thread, before anything adds: this frame's become last frame's to read.
    public void turn()
    {
        tally.heard = min(tally.written, n);
        tally.written = 0;
    }
};

/// A pool of n things, each in a place of its own from birth to death, and the live ones this
/// frame: [Size(n)] Pool<T> name;. Take a place for a birth, give it back at a death; list a
/// thing as live each frame it lives, after the frame's turn (one thread, first).
///
///     pool.turn();                           // one thread, first: no live ones listed yet
///     uint slot;
///     if (pool.take(slot)) pool.put(slot, born); // a birth, if a place is free
///     T now = pool[i];                       // thing i
///     pool.give(i);                          // a death: place i free from the next pass on
///     pool.live(i);                          // thing i lives this frame
///     uint i = pool.living(k);               // the k-th live one, k < pool.count
[Block(n)]
public struct Pool<T>
{
    public uint n;
    [Size(n)] List<T> items;
    [Size(n)] Slots places;                   // the free places
    [Copies] [Size(n)] Gathered<uint> alive;  // this frame's live ones: what copies: pool draws

    /// The places in the pool.
    public property uint capacity { get { return n; } }

    /// How many are listed as live this frame.
    public property uint count { get { return alive.count; } }

    /// A free place into slot; false (slot 0) when none is.
    public bool take(out uint slot) { return places.take(slot); }

    /// Hands place i back.
    public void give(uint i) { places.give(i); }

    /// Lists thing i as live this frame.
    public void live(uint i) { alive.add(i); }

    /// The k-th live one's place.
    public uint living(uint k) { return alive[k]; }

    /// From one thread, before anything lists: no live ones yet this frame.
    public void turn() { alive.clear(); }

    /// Thing i.
    public __subscript(uint i) -> T { get { return items[i]; } }

    /// Puts value in place i. (A block reaches a stack as a value, so it is put, never assigned.)
    public void put(uint i, T value) { items[i] = value; }
};
