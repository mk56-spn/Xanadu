# Design (v4)

## Chords

- Chords are a non-permanent set of diagonal lines down which notes fall.
- Chords are an indicator of what's coming.
- For now, a chord comes in at 50° from the horizontal (40° off the vertical).
- Chords are non-permanent because we often want long drops during downtime,
  and don't want to tie ourselves to a confined region.
- The camera for this is handled later.
- In a chord, all notes have a single height at which they expect an impact.
- For the first build, a chord has one line per action key, evenly spaced, all
  leaning the same way at 50°.
- A chord sits still in the level; the running player passes through it.
- The judgement line is horizontal; the mapper sets each chord's impact height.
- A note takes 700 ms from entering its chord line to its hit time. Variable
  scroll speed comes at some point.

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
- The entrance is its own special note type; it doesn't hijack the first note.
- It has a timing window like any other note, and launches the player out at
  ±0 ms regardless of how it was hit.
- The judgement is there because music is happening and the player should
  have something to hit.
- The player doesn't exist until the entrance releases them.
- Some songs don't want to begin on a fall: some want to start on a dash, or
  a slide (riding an orb).
- So the entrance needs to be able to change its exit: it's a special onset.

## Input

- Keys stay as they are: directions on F/J/K/L, actions on S/Space/D.
- Hitting a note: the player releases a beam down into the horizontal
  judgement line, and that hits the note, wherever on the x or y axis the
  player is.

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
- After popping out of the entrance, the player runs right at that fixed
  speed.
- Gravity applies.
- The right edge is a wall (no wrapping).

## Hazards

- Spikes can stop the player landing somewhere.
- Saws: spikes, but circular, for when that better suits the level or
  aesthetic.

## Dashes

- Dashes are marked as a line across the chord.
- Dash notes are directional: the line through the chord lets the player
  choose the direction.
- You aren't told what to do with them; that's on you as the player.
- Dashes have no set ending.
- A dash ends on either:
  - a wall
  - a dash end marker (like Geometry Dash's S block)

### Dash exit pads

- Sometimes (usually) we don't want to just drop vertically when a dash ends.
- Pads that the player doesn't trigger can impart momentum onto the exit of a
  dash.
- They sit at the end of a dash, but aren't attached to it: they're freely
  placeable.
- Think of a Geometry Dash pad, imparting vertical momentum.
- A small difference from Geometry Dash: not really for dashes, but in some
  situations, like falling onto them, we want the impulse on the exact moment
  of a note. They activate on time, not position.

## Updrafts

- An upward draft emitter, to avoid landings in low-input sections.
- Emitters are a fixed diameter.

## Actions

- Now that movement feels goodish, actions have a problem: they don't really
  do anything.
- This is the gap where v1's weapons and character types can come back.

## Open questions

### Input

3. Is there a jump, or gravity only? What do the direction keys do?

### Chords

4. How far apart are a chord's lines?
5. Which way do chords lean (falling to the right or the left), and can it
   change between chords?
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
