# Design

## Core

### Pillar: throughput

- Managing several input streams at once is the game's identity.
- Don't simplify rules to lower the input ceiling.
- Difficulty is a mapping decision; the rules stay the same across difficulties.
- Skill expression is scheduling: reading ahead and choosing what to do when.

### Input

- 7 keys: 4 directional keys and 3 action keys.
- Default binds:

| Role | Keys |
| --- | --- |
| Left / right | S / D |
| Up / down | K / L |
| Actions | F, Space, J |

- Players can rebind keys as they please; the defaults don't matter much.
- Why not WASD: it can't handle high-speed pressing.

### Directional notes

- Directional notes are agnostic: they don't say which key to press. The player decides.
- They never show which exact direction to go.
- A note's type may show the set of directions: a normal directional note means one of the 4 straight directions; a diagonal note (option A) means one of the 4 diagonals.
- Why: it lets mappers decide how much assistance to offer.
- Two flavours: **dash** and **momentum**. Dash works as the existing dash does, with the preset direction removed.
- Each note still owns its strength: a dash owns its distance, a momentum note owns its impulse.
- Dash destination is measured from the note.
    - Exception: hit while on a surface (the floor or a block), the vertical placement is measured from the player's vertical centre instead.
    - Why: so short vertical dashes near the floor don't do next to nothing.
- A momentum note imposes a given vertical and horizontal speed. It does not stack.
- Notes show their strength.
- After exiting a dash, gravity is briefly neutralised or slowed so the player doesn't drop like a rock. The amount is small; what feels right will be tested later.

### Movement

- Inputs own the direction of movement. Notes own its strength.
- The direction the player presses decides the movement.
- A wrong press is wrong because of where it sends the player. Example: pressing left on a dash that needs up sends the player into a wall of spikes, and they die.
- A wrong-direction press still counts as a hit.
- The player reads the right direction from the level.

### Ground movement (two options, undecided)

- v2 builds both behind a toggle, to test and pick.

- Friction is ruled out as a default: the misalignment it causes builds up quickly.
- **Option 1 — skid:** no friction by default. Landing on the floor, the character skids, keeping whatever lateral speed it landed with.
    - If chosen, there must be a **friction field**: it enables friction and brings you to a stop.
    - To consider: should a friction field stop you on the x axis at the same point regardless of entry speed?
- **Option 2 — hold:** to keep moving on the ground, you must hold the direction key you hit the directional note with.

### Stationary character

- The character standing still is a long-standing problem.
- **Opening:** the character falls into the map.
- **Slopes:** landing on a slope snaps you to a set speed.
    - The speed is not decided by the mapper (slope speeds varying per map would be awful to play).
- **Wall slide:** a new action surface lets you slide down walls.
    - It's a flat note embedded onto a surface. Making it look good is the mapper's job.
    - Its hitbox protrudes a little from the surface. Hit it and you stick to the wall, and wall particles trigger.
- Slopes and wall slides keep you engaged during slow sections.
- **Action notes** give a little impulse.
    - Being reconsidered: should action notes move the character for real?
    - Alternative: the shadow attack that already comes out of you actually launches itself in the note's direction.
    - Trying the ghosts for now.
- An impulse cancels a dash's hang.
- Accepted: early-era mapping will look bad in retrospect.

#### Built for testing (defaults to confirm)

- Opening: she starts at the top of the field and falls when the song starts. Start points still place her.
- Slopes: a block can be a ramp (L in the editor). One speed for every slope, snapped to on the surface, downhill.
- Wall slide: a wall note kind, touched within its beat's window. No press, not scored. Slide speed fixed, snapped to on contact. Ends on landing, when the wall ends, or on a directional note.
- Action notes: the hit ghost launches toward the note and a little past it; the character stays.

### Action notes

- Coloured action notes come in 3 colours. Each action key hits one colour.
- The colours only need to be impossible to confuse with each other.
- **Neutral action notes** carry no colour. Any action key hits them; the player picks which.
- Neutral notes are for every character, for now.

### Chords

- An action note can be combined with a directional note to form a chord.
- A chord is judged as however many notes it has.
- A chord doesn't tell you a direction.
- Later: a visual that shows notes are in a chord (visual assistance, not core).

### Diagonals

- **Active — A. Diagonal offset note:** a rhombus (a square rotated 45°). Hit with a single direction input; the note offsets that input by 45°, so it sends you diagonally.
    - The offset is clockwise: up → up-right, right → down-right, down → down-left, left → up-left.
- **Not active — B. Two-input note:** takes the two direction keys that combine into the diagonal (e.g. up + right). Kept in case.

### Mapper assistance

- The mapper guides the player with arrow icons or any other context they consider sufficient and viable.
- Icons are regular placeable text / decoration, as in Geometry Dash. They are not part of a note.

### Player character

- The character is humanoid. On the way down from a jump she pulls her legs up, as a human does.
- Spikes should not hit you far below your feet on the way down; this is the cost of a humanoid.
- So a smaller hitbox is applied on the way down.
- The player has two hitboxes, so to speak.
- Landing is judged at her usual hitbox height: that decides when she switches to a standing pose and is set to landed.
- Why: people don't land on their knees, and it avoids her suddenly popping up.
- If you land in spikes, you get hit. Avoiding that is the mapper's job.
- Lateral collisions use the small hitbox.

### Removed

- Pair notes.

### Judgement and scoring

- All notes are judged at the same accuracy weight.
- Accuracy and combo work as before.

### Failure and death

- Same rules as before: mechanics never kill.
- Only mapper-placed lethal geometry kills: spikes and death boxes.
- No health model.
- For now, death sends you to the start. To reconsider once a robust start position system is in place.

### Characters

- v2 has Contact only. Armed, Splitline and Form are removed and will be redone later.
- More characters will be added. They will switch; how is TBD.

### Mapper contract

- Mostly TBD.
- Landing in spikes hits the player; the mapper places spikes with that in mind.

## Contact

- Contact keeps its current hitbox (the circular melee hitbox).

### Action bars

- The left/right split is replaced by one based on the action keys.
- 3 contact bars, one per action key.
- The action key you hit a neutral action note with decides which bar it loads.

### Barriers

- Barriers check your bars.
- A barrier's cost is whatever the mapper sets, within limits:
    - up to 4 of each bar type;
    - at most 2 bar types per barrier.
- A barrier spends the bars it checks.
- If you're short, nothing is spent.
- A fail costs accuracy.
- What a pass does (score or otherwise) is TBD.

### Bars

- Only neutral action notes load bars. No other notes do.
- Bars don't decay.
- Each bar caps at 5.

### What the player sees

- Each barrier's cost.
- How full each of their 3 bars is.

### Design intent

- Imposing choices.
- Example: you know you need to load 3 on the middle key's bar. How and when you do that between this barrier and the next is up to you.

## Conflicts

None open.

## Open questions

- Smaller hitbox: when does it switch on and off (any fall, or only after a jump; until landing)?
- Does a barrier fail also break combo?
- Ground option 2: what happens when you let go — stop at once, or slow down? Is the hold judged?
- Option B: judged as one note or two (chords count each note)?
- Slopes: one speed for every slope, or does it follow the slope's angle?
- Wall note: should it be judged (scored) like a note, or stay a touch like a Contact barrier?
- Action notes: after the ghost trial, should they move the character for real?
