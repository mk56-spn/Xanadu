# Character design

The authoritative design for Xanadu's characters, note vocabulary, hit rules, and
mapper contract. It supersedes the earlier character sketches.

**Status:** note taxonomy, Contact, Armed, Splitline and Form are implemented.
Remaining tuning/open items are listed in
[Open questions](#7-open-questions).

---

## 0. Overview

Xanadu is a rhythm-action game played on six side keys — L1, L2, L3, R1, R2,
R3 — plus M. Charts are authored by a mapper. The mapper owns note placement,
movement, hazards and difficulty; the game owns judgement, character rules and
feedback.

There are four characters:

| Character | Special note | Hit shape |
| --- | --- | --- |
| **Contact** | Contact barriers / charge checks | Circular melee hitbox |
| **Armed** | Weapon-switch note | Spear line or battle-axe cone |
| **Splitline** | Channel notes | Side rails |
| **Form** | Construction notes | Standard hits |

One character is active at a time, and a special note switches between them
mid-chart.

Two rules mirror each other and form the core thing the player learns to read:

- **Pair notes check index** (1–3) and are side-agnostic.
- **Contact barriers check side** (left/right) and are index-agnostic.

Use pair notes to decide which side you are building; read barriers to know
which side you will need.

### Design intent: throughput

Xanadu is deliberately process-throughput heavy. Managing several simultaneous
input streams — notes, side stacks, a rail, a construction, a weapon arc — is the
game's identity, not an accident of scope.

This is a pillar, not an accessibility bug:

- **Do not simplify a mechanic's rules to lower the input ceiling.** The ceiling
  is the point.
- **Difficulty is a mapping decision.** A chart can introduce one character kit,
  or use only one kit, and still be a complete chart. Easier charts use fewer
  characters and fewer simultaneous systems; harder charts use all four.
- **The map's own note density and patterns** provide the rest of the difficulty
  curve. The character rules stay constant across that curve.
- **The player's skill expression is scheduling.** Reading ahead, choosing which
  side to feed, when to switch, when to build, and when to bank a contact charge
  is the mastery.

---

## 1. Input

### 1.1 Keys

- **L1, L2, L3 / R1, R2, R3** — six side keys, indices 1–3.
- **M** — keeps a normal keynote for now. Its eventual special role is undecided.
- A **pair index** is 1, 2 or 3. L# and R# are the pair.
- A **side** is left or right.

### 1.2 Judgement

- Normal, pair, dash, momentum, character-switch, weapon-switch and channel-start
  notes are keyed notes judged on the standard timing windows.
- Contact barriers are automatic checks, not key presses.
- Rail taps and holds are judged like notes.
- Form construction steps are judged on their beats.
- A missed keyed note is a miss.
- A character-specific special belonging to an inactive character is inert and
  is missed; it does not take the input.

### 1.3 Press resolution

These are the implementation defaults for one press:

1. Look for a note the press can hit — matching key (or pair side), inside the
   active character's hit shape.
2. If one or more qualify, hit the nearest/best one.
3. If no note qualifies, the press may claim an eligible dash or momentum note.
4. Only one note is claimed per press, except for Armed's same-key swing.
5. A pair note is judged once. Either side can claim it; if both are pressed at
   once, the first claim wins and the other press is a separate input.

Any-key movement notes are therefore only claimed when the press would not
otherwise hit a note.

---

## 2. Notes

### 2.1 Normal keynote

- Requires its specific key.
- Judged once.
- In Contact mode a successful hit adds **1** to the pressed side's contact
  stack.
- M has no side, so an M hit adds nothing to Contact.

### 2.2 Pair note

- Has an index 1–3.
- Hit by **either** L# or R#. The note itself is side-agnostic.
- Judged once.
- In Contact mode the side that claimed it adds **1** to that side's stack.
- This is the decision lever: hitting every pair note with the same side builds
  one stack and starves the other. Upcoming barriers punish that.
- Uses the active character's normal hit shape.

### 2.3 Dash note

- A separate keyed note. It cannot also be a normal keynote.
- **Any key claims it** (the six side keys; whether M also counts is open).
- Authored per note:
  - one of eight directions;
  - one of three distances.
- Effect: teleport to **note centre + direction × distance**. The destination is
  anchored to the note, not to the player's position.
- Passes through blocks.
- Grants **no invulnerability**.
- Hazards at the destination resolve normally; whether the path is checked is
  open.
- Missing the press means no movement and a missed note.
- Judged like a note.
- Adds nothing to Contact stacks.

### 2.4 Momentum note

- A separate keyed note. It cannot also be a normal keynote.
- **Any key claims it.**
- Authored per note:
  - one of eight directions;
  - one magnitude.
- Effect: one instantaneous impulse in the authored direction.
- Not sustained.
- Momentum decays through the normal ground friction. There is no special air
  decay.
- Normal collision applies; no invulnerability.
- Missing the press means no impulse and a missed note.
- Does **not** clear downward momentum — only the spear does that.
- Judged like a note.
- Adds nothing to Contact stacks.

### 2.5 Character-switch note

- Universal special note, not tied to a character.
- Hit with its authored key like a normal keynote (default).
- Switches the active character to the authored target.
- Judged like a note.
- Position and velocity are preserved.
- Contact stacks are a run resource and persist across the switch.
- The outgoing character's weapon/channel/construction state does not transfer.
  Exact reset behaviour is open.
- Mapper contract: do not place a switch during an active channel or
  construction, and do not strand the player in a character whose specials are
  required before the next switch.

### 2.6 Special-note gating

- Contact barriers, channel notes, weapon-switch notes and construction notes
  only function while their character is active.
- For any other character the special is **inert**: it does not take input, it
  cannot be resolved, and it is missed.
- Well-formed charts place a special only while its character is active.
- Character-switch notes are universal.

---

## 3. Characters

### 3.1 Contact

**Hit shape.** A circular melee hitbox around the character. It replaces the
standard reach for every note Contact hits.

Key matching still applies: a normal keynote needs its key, and a pair note
needs either side of its index. The circle is the range check, not a multi-hit;
one press resolves one note.

**Contact stacks.** Two side pools: left and right.

- A successful normal keynote adds 1 to the side of its key.
- A successful pair note adds 1 to the side that claimed it.
- M and any-key movement notes add nothing.
- Pools persist for the run. No decay and no cap for now.
- Only barriers consume them.

**Contact barriers.**

- A barrier is a **charge check**, not a keyed note and not a timing press.
- It has an authored position and resolves automatically when Contact's melee
  circle overlaps it.
- It carries at most **two costs**. For now the two costs are **left** and
  **right** amounts. The cap is a design limit; a future version could target
  two specific keys.
- On resolution it drains those amounts from the side pools as far as they can
  be paid.
- It then checks whether both costs were fully paid.
  - **Fully paid:** the barrier is passed.
  - **Short:** the barrier fails. The player takes an accuracy penalty and the
    combo breaks. The barrier never kills directly.
- Costs are consumed whether the check passes or fails. Excess stacks remain in
  the pool.
- The signature failure: three L1 hits leave 3 left and 0 right. A
  2-left / 1-right barrier drains 2 left, fails the right check, and leaves
  1 left.
- A barrier that is never overlapped simply expires; it is not a scored miss.
  The mapper places hazards when avoidance should be fatal.
- Barriers are checks, not scored notes; they are not part of the note total.
- A failed barrier costs 0.02 accuracy per scored note and breaks combo.

**Death.** Not the barrier's job. The mapper places spikes or death boxes when a
missed barrier should lead to death.

**Read.** The player should be able to see the two costs and their current
left/right stacks. The barrier is the mirror of the pair note: pair note =
index, barrier = side.

### 3.2 Armed

**Weapons.** Armed carries one weapon at a time.

- **Weapon-switch note:** any single **left** key selects the spear; any single
  **right** key selects the battle axe. The note is judged once and switches
  immediately.
- Only Armed can use the switch note.
- Armed starts with the spear.

**Swing resolution.**

- A press must match a note's key, as normal. That matching note must be inside
  the current weapon's hitbox; it is the first hit.
- The swing then also hits every other note at the **same authored timestamp**
  that is inside the weapon hitbox **and matches the same press** (the same key,
  or the same pair index for pair notes). A swing never steals another key's
  note.
- Notes at that timestamp outside the hitbox are missed.
- One press is one swing.
- The swing uses the character's facing direction.

**Spear.**

- A long, thin horizontal box extending forward from the character.
- Much longer forward reach than the standard hit shape.
- No self-knockback.
- On a successful hit, clears the character's current downward momentum (sets
  downward velocity to zero).
- Good for reaching forward and for catching a fall.

**Battle axe.**

- A 180° cone in front of the character with an inner deadzone — a
  forward-facing annulus sector.
- It is about hitting an arc, not about what the character is standing on; the
  deadzone means notes overlapping the body are not hit.
- On a successful hit, the character is knocked back opposite its facing at
  350 px/s. The axe does not clear downward momentum.
- Dimensions: spear 280 × 26 playfield units; axe inner radius 60, outer radius
  260, forward half-plane.

**Movement.**

- Armed uses the same dash and momentum notes as everyone else for now.
- Its only movement differences are the axe's self-knockback and the spear's
  downward-momentum cancel.

### 3.3 Splitline

**Channels.** Splitline has two channels: left and right.

- A channel start is a `Channel` note on the playfield, hit with its key. It
  absorbs that side.
- Channels and their rails live in a **separate rail layer** in the chart: rail
  taps and holds, plus a channel-end marker. Rail notes carry a side, one of that
  side's three keys and a hold length (zero for a tap).
- Think of a channel as a **non-held hold note**: it keeps an input to itself
  for a time, but the player is still performing taps and holds on a rail.
- While a side is absorbed:
  - its three keys are owned by the channel;
  - that side's normal playfield notes are replaced by a rail authored in the
    chart;
  - the rail uses that side's keys. Circles indicate taps; a bar or line
    indicates holds. The player performs them in time;
  - rail taps and holds are judged and scored like notes;
  - the other side continues to play normally;
  - the absorbed keys cannot hit playfield notes or claim movement notes.
- The channel ends at the next end marker for that side, or 4 seconds after the
  start when the mapper did not author one.
- A second start on an already-absorbed side refreshes its end.
- Rail holds score when the hold begins; releasing early applies a 0.01 accuracy
  penalty and breaks combo. Reaching the end clears the hold silently.
- Channel starts and rail notes count as scored notes; end markers do not.

**Both channels absorbed.**

- The character is removed from the playfield entirely. It is not present to
  make contact, so it is **invulnerable**.
- Music, time and scoring continue. The player performs both rails.
- The mapper must not author anything that requires the missing character: no
  normal playfield notes, no contact barriers, no movement notes requiring the
  absorbed keys, no hazards that need contact.
- The character returns when one channel ends, at the position it left.
- With only one channel absorbed, the character is still present and can be hit.

**The balls.**

- Two balls, one per channel, indicate which channels are available.
- While idle they lerp-follow the character.
- When both channels are absorbed the balls are hidden with the character.
- They are visual only.

**Mapper contract.** Author the rail layer separately: rail taps and holds plus
channel end markers. Place channel starts on the playfield with the rail layer
panel's playhead. Do not place impossible notes while a side is consumed; the
same courtesy as not placing a normal key during a hold in osu. A modicum of
mapper intelligence is assumed.

### 3.4 Form

**Hit shape.** Form uses **standard hits** for normal and pair notes. It has no
special hit shape for ordinary notes.

**Construction.**
- The mapper selects existing blocks and attaches them to a new shape; the
  selection order becomes the reveal order. A `Construction` marker note starts
  the shape when hit.
- The sequence is authored as ordinary keyed `ConstructionStep` notes, ordered
  by their chart time. Each step has one of the shape's pieces.
- Attached blocks are hidden and non-solid until their step lands. A revealed
  piece becomes solid immediately and stays solid for the run.
- A step is inert until its shape's marker has been hit. Hitting a step reveals
  its piece; the last step clears the shape's active state.
- A wrong key does nothing; the step remains until it misses.
- Dashes pass through blocks as usual, so the shape catches normal movement and
  momentum, not dashes.
- The mage half of Form is deferred.

**Mapper contract.** The sequence consumes keys; do not overlap it with
unavoidable notes or hazards that require those same inputs. The mapper maps the
shape and the survival problem the shape solves.

---

## 4. Global rules

### 4.1 Hit resolution summary

| Note | Key | Hit shape | Judged | Contact stack |
| --- | --- | --- | --- | --- |
| Normal | specific | character shape | yes | +1 to that side |
| Pair | either side of index | character shape | yes, once | +1 to the claiming side |
| Dash | any key | — | yes | no |
| Momentum | any key | — | yes | no |
| Character switch | authored key | universal | yes | no |
| Weapon switch | any left/right key | special | yes | no |
| Contact barrier | automatic overlap | Contact circle | check | consumes |
| Channel note | authored key | special | yes | no |
| Rail tap/hold | side key | rail | yes | no |
| Construction step | authored key | standard | yes per step | no |

- Only one note per press, except Armed's same-key swing.
- Any-key notes are claimed only if the press did not hit a note.

### 4.2 Failure and death

- Mechanics do not kill. Contact shortfalls, misses and failed constructions
  apply accuracy/combo penalties.
- Death comes only from mapper-authored lethal geometry: spikes and death boxes.
- The mapper supplies that geometry when a mistake should be fatal.
- While both channels are absorbed, the character is absent and invulnerable.
- There is no universal health/damage model in this design.

### 4.3 Scoring

- Normal, pair (once), dash, momentum, character switch, weapon switch, channel
  start, rail notes and construction steps are scored notes.
- Contact barriers are checks, not scored notes; they are not part of the note
  total.
- Contact shortfalls cost 0.02 accuracy per scored note and break combo.
- A missed special of the wrong character is a miss.

### 4.4 Character switching

- One active character.
- A switch note changes the active character immediately.
- Position/velocity are preserved; contact stacks persist; weapon/channel/build
  state do not transfer (exact reset open).
- Foreign specials are inert and missed.
- The mapper should not switch during a channel or construction, nor place
  specials for a character who is not active.

---

## 5. Mapper contract

The mapper authors:

- note times, positions and keys;
- dash directions and distances;
- momentum directions and magnitudes;
- contact barrier costs (left/right) and placement;
- channel sides, rails, taps, holds and ends;
- weapon-switch notes (side chooses the weapon);
- construction sequences and shape stages;
- character-switch targets;
- spikes and death boxes.

The mapper must not:

- place a character special while that character is not active;
- place normal notes on an absorbed side;
- place movement notes that need absorbed keys;
- place contact barriers or body-dependent notes while both channels are
  absorbed;
- overlap a construction sequence with unavoidable notes;
- switch characters mid-channel or mid-construction;
- expect the game to kill for a mechanical failure — author the hazard instead.

Good charts use the pair/barrier mirror: pair notes force a side choice;
barriers check it.

Difficulty is regulated in the chart, not by trimming the rules: use fewer
character kits, fewer simultaneous systems and lower note density for easier
charts; use all four and overlap systems for the intended ceiling.

---

## 6. Visual language

The goal is that each note role and character special is readable at speed
without reading text, and that four kits do not look like noise. This is
implementation-owned, but the intent is:

- **Normal** — keycap glyph.
- **Pair** — index badge shared by both sides; both side colours or a split cap;
  the pressed side lights up.
- **Dash** — direction arrow and distance pips (1/2/3).
- **Momentum** — direction arrow and a weight/impact glyph.
- **Character switch** — target character portrait/icon.
- **Contact barrier** — a ring/charge gauge showing left and right costs and the
  remaining stacks; passing reads as a discharge.
- **Channel** — a side-coloured rail with tap circles and hold bars; the active
  ball attaches to or leads the rail.
- **Weapon switch** — spear/axe icon with a left/right hint.
- **Construction** — ordered keycaps on their beats, with the shape growing
  piece by piece as they land.
- **HUD** — left/right contact stacks, current weapon, channel availability,
  construction progress.

Shape, colour and motion should all distinguish; never colour alone. The
character silhouette/stance should also telegraph its hit shape — circle, spear
line, axe arc, rails.

---

## 7. Open questions

1. **Character switch:** accepted key(s), whether it has a cost, and exactly
   which state carries (weapon, channels, partial builds).
2. **M:** its eventual role; whether it should feed Contact (currently no side).
3. **Contact:** whether multiple barriers may resolve at once; exact authored
   cost ceiling (the editor currently allows 0–3 per side).
4. **Movement notes:** whether they need proximity; whether multiple momentum
   notes stack; whether M claims them; whether a dash checks hazards along its
   path or only at its destination.
5. **Armed:** whether weapons hit mines or other specials; switch timing and
   mid-attack switching; whether the axe's knockback should also have a vertical
   component.
6. **Splitline:** whether a channel start should be scored (currently it is);
   ball visuals while a channel is active; whether a second start should refresh
   or be rejected (currently it refreshes).
7. **Form:** whether a wrong key should fail the step (currently it is ignored);
   whether partial pieces should be solid immediately (currently they are);
   whether multiple shapes can be active at once (currently one); whether steps
   can be pair notes.
8. **Global:** exact judgement/penalty numbers; the definition of a death box;
   the visual language pass; how charts onboard each kit (a mapping/tutorial
   concern, not a rules concern).

---

## 8. Implementation order

1. Note taxonomy + pair/dash/momentum + character-switch note, including editor
   and level-format support. — done
2. Character special gating + shared hit-shape abstraction + switch. — done
3. Contact: side pools, barriers, circle, HUD. — done
4. Armed: weapon switch, spear/axe hitboxes, knockback and downward-momentum
   cancel. — done
5. Splitline: rails, input takeover, both-absorbed state, balls. — done
6. Form: beat sequences, staged shape growth, solidity. — done
7. Visual language, authoring pass, charts, verification.
