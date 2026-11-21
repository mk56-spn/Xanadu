# Design (v4)

## Chords

- Chords are a non-permanent set of diagonal lines down which notes fall.
- For now, a chord comes in at 50° from the horizontal (40° off the vertical).
- Chords are non-permanent because we often want long drops during downtime,
  and don't want to tie ourselves to a confined region.
- The camera for this is handled later.
- In a chord, all notes have a single height at which they expect an impact.

### Playfield

- The playfield needs a way to work with this: how do we handle a player that
  moves in the y direction, but a line onto which notes fall?
- The vertical impact lines on notes (formerly aesthetic) now come from the
  player.
- They impact on the judgement line, above or below the player, where the two
  intersect.

### Entering the playfield

- No camera for now.
- Black is the default background colour.
- To help the level start, it fades in from black.
- The player descends on a platform going down the first chord.
- The platform smoothly moves out to the middle, leaving the player in the
  centre for the first note.

## Notes

- Notes are rectangles, like in BMS.

### Hold notes

Holds work like the ORBS lab.

- A hold is an orb.
- Every orb moves sideways at one fixed speed; its height is free, and follows
  the shape of the hold.
- Hitting the hold takes the player instantly to the orb.
- Holding the key rides it: the player hangs from the orb.
- Letting go leaves with the orb's sideways speed and its vertical speed at
  that instant, and keeps both.
- After that, gravity takes over.
- Holds need proper starts and ends, like a hold note has.

## Player

- For now, the player has a single speed.

## Hazards

- Spikes can stop the player landing somewhere.
- Saws: spikes, but circular, for when that better suits the level or
  aesthetic.

## Dashes

- Dashes are marked as a line across the chord.
- You aren't told what to do with them; that's on you as the player.
- Dashes have no set ending.
- A dash ends on either:
  - a wall
  - a dash end marker (like Geometry Dash's S block)

### Dash exit pads

- Sometimes (usually) we don't want to just drop vertically when a dash ends.
- Pads that the player doesn't trigger can impart momentum onto the exit of a
  dash.

## Open questions

### Input

1. What are the keys? Does F/J/K/L + S/Space/D stay, or is it new?
2. What counts as hitting a note? Pressing a key as it reaches the judgement
   line where your impact line crosses it?
3. How do you move? Hold left/right at the single speed? Is there a jump, or
   gravity only?

### Chords

4. How many lines are in a chord, and how far apart?
5. Do chords only lean one way (50° from the horizontal, falling to the right),
   or either way?
6. Is the judgement line horizontal, and how does the mapper set its height?
7. What happens between chords? How does one end and the next appear?

### Dashes

8. What starts a dash: a key, or crossing the dash line? Which direction does it
   go, and how fast?
9. What does a pad set: direction and strength, placed by the mapper?

### Hold notes

10. How do they start and end? Is the start a normal note hit, and what ends
    one?
11. Is anything judged during the ride, like holding a key?
12. Do they replace the old tracked-hold vs sustain split, or is sustain still a
    separate thing?

### Hazards and scoring

13. Does touching a spike or saw kill you, or cost something?
14. Do judgement windows and scoring carry over from before, or start fresh?

### Editor

15. Which of these does the mapper place by hand (chords, dash lines, end
    markers, pads, hold paths, hazards), and which come from the notes?
