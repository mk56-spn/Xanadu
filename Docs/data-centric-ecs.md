# Data-centric ECS

Status: **phase 0 and phase 1 done 2026-11-23; phase 2 in progress (§13: steps 1–3 and `ToolAction` done).** This doc is the hand-over:
whoever picks this up (human or agent) should be able to start from here alone.
Nothing below has been built yet except where it says so.

## 1. The problem in one paragraph

We use the ECS the way we used to write classes: **one component per idea**.
Five animator dialogs each got their own component holding exactly `bool Open`.
`ShapeGlow` and `TextGlow` are the same three fields under two names. A tree
row, a dropdown, an editor button and an animator button each invented their
own way to say "do this to that", and because none of those ways was ECS, they
fell back to envelopes: a kind enum plus loosely typed slots (`ToolAction`'s
string argument, `EditorCommand`'s seven optional fields, `UiCommand.Arg/Text`).
The string passing fixed on 2026-11-23 was a symptom. The cause is idea-centric
components, plus having no ECS-native way to send an intent.

## 2. The principle

> **A component is a shape of data, named for what the data *is*, never for the
> feature that owns it.** What the data is *about* comes from the entity it
> sits on: its tags, its links, its place in the entity tree. Two features that
> store the same thing use the same component.

Corollaries, each one a rule:

1. **Bool-only components are tags.** `Open`, not `AnimatorInfoPanel { bool Open }`.
   A component with no fields at all is a tag.
2. **Same shape, same component.** A position is `Position` whether it is a
   note's, a pad's or a deco's. The kind of thing is a tag (`Note`, `Pad`) or a
   role component (see [generic gameplay roles]: objects are role components plus
   data/tags).
3. **"Which one" is a link or a tag, never an index or a name.** Selection is a
   `Selected` tag on the selected things. "This control edits that bone" is a link
   from the control to the bone entity. Indexes into a C# list (`int Bone`,
   `int Attachment`) and string ids (`"part:3"`) are banned between systems.
4. **Intents are typed signals sent to an entity** (Friflo `Signal<T>`, see §4),
   not components, not envelopes, not C# interfaces held in a component.
   A signal payload is a small record carrying exactly its meaning
   (`Pick(bool Additive)`, `SetRotation(float Radians)`). Signals are not
   components, so they cost none of the 255 component slots.
5. **No handle bags.** A component that is a list of child `Entity` handles
   (`EditorHud`, `AnimatorUi` with ~40 of them) is a class in disguise. Children
   are found through the entity tree, a role tag, or an incoming link.
6. **No host interfaces in components.** `TimelineHost(ITimelineHost)`,
   `XifEditor.Host`, `EditorShortcutHost` and similar are non-ECS channels:
   method calls hidden in a component. They become state on entities plus signals.
7. **Text exists only where a person typed it or a file stores it.** It is turned
   into entities or values once, where it comes in (an `IIndexedComponent<string>`
   name lookup, a typed field parse), and never travels further as text.
8. **Data the game owns lives in the ECS.** The rig (bones, parts, chains, cloth,
   clips, keys) becomes entities as the single source of truth (user's decision,
   2026-11-23, "option 1"); Serialisation/Storage convert at the file edge.
   A mirror that is rebuilt from a C# object is not acceptable (two sources of truth).

## 3. Measured state (2026-11-23)

Method: every `struct ... : IComponent | ITag | ILinkComponent | IIndexedComponent`
outside tests and `External/`, fields read from the source. Re-run against the
code before acting; `~/.cache/xanadu/atlas.txt` (DevTools ATLAS) has live counts.

- **228 types**: 188 components, 2 link components, 38 tags. Model 120, UI 81,
  Core 15, Tools 8 (Labs), Screens 3, Logic 1.
- **Budget**: Friflo caps 255 components and 255 tags per process (see the
  `ecs-type-limits` memory). The heaviest process is the animator, at 183/255
  components and 39/255 tags. Since the 2026-11-23 split the game process
  loads the animator's components too (they live in Model now), so it is at the
  same figure.
- **53 of 188 components share their exact field shape with another one.**

### 3.1 Exact duplicate shapes

| Shape | Components |
|---|---|
| `bool` | `AnimatorClipLength`, `AnimatorNewClipDialog`, `AnimatorEasingWindow`, `AnimatorShortcutsPanel`, `AnimatorInfoPanel` (all `Open`), `EditorMetronome` (`Enabled`), `CharacterFacing` (`Left`) |
| `int` | `DropdownChanged`, `Drawable`, `SongSelectTile`, `ChartIndex`, `AnimatorTargetCaption` (`Layer` is the indexed int) |
| `string` | `UiTooltip`, `Text`, `UiTreeChanged`, `WarningRequest`, `CharacterRigSource`, `LevelFile` |
| `float` | `UiRotation`, `UiChamfer`, `LayerOpacity`, `ScrollView`, `Character` |
| no fields (should be tags) | `UiBackground`, `EditorNoteBarPanel`, `EditorSongLabel`, `SpectrumEnergy` |
| `Vector2` | `Position`, `DashStop`, `Player` |
| `Color` | `SelfModulate`, `FillColor` |
| `Color, float, float` | `ShapeGlow`, `TextGlow` (same fields, same meaning) |
| `Rect2` | `Rectangle`, `EditorSidebar` |
| `Vector2, float` | `Draft`, `AnimatorPreviewView` |
| `int, int` | `Playfield`, `AnimatorHover` |
| others | `UiImage`/`VisualItem` (Handle), `UiMeter`/`PlayerTuck`, `UiAnchor`/`PlayerSmoothing`, `UiPose`/`PlayfieldView`, `EditorLeftMenu`/`TimingIndicator` (one Entity) |

Some of these are coincidences of shape with different meanings (`Playfield`
width/height versus `AnimatorHover` bone/part). Same shape is a prompt to
check, not an automatic merge. **The merge test is: same meaning, or does a
data name exist that honestly covers both?**

### 3.2 The same field under many component names

| Field | Count | Components |
|---|---|---|
| `Color Color` | 11 | `UiBarGraph`, `SelfModulate`, `ShapeGlow`, `UiColorDrop`, `UiConnector`, `TextStyle`, `TextGlow`, `TextOutline`, `UiBorder`, `HitTrail`, `NotePop` |
| `bool Open` | 10 | `Dropdown`, `XifEditor`, `AnimatorClipLength`, `AnimatorNewClipDialog`, `AnimatorEasingWindow`, `AnimatorShortcutsPanel`, `AnimatorInfoPanel`, `AnimatorKeyMenu`, `AnimatorBoneMenu`, `EditorToolGroup` |
| `Vector2 Position` | 9 | `ParticleBurst`, `Note`, `CurveBody`, `Pad`, `Draft`, `DashStop`, `HitTrail`, `NotePop`, `Deco` (plus `Position` itself) |
| `bool Active` | 6 | `UiMarqueeBox`, `AnimatorMeshPick`, `AnimatorWeightPaint`, `AnimatorAttackCycle`, `AnimatorKeyTryout`, `AnimatorSequence` |
| `int Index` | 6 | `UiValueSlot`, `SongSelectTile`, `ChartIndex`, `AnimatorAttackCycle`, `AnimatorSequence`, `EditorToolGroup` |
| `string Path` | 5 | `WaveformPreview`, `EditorRecoveryOffer`, `CharacterRigSource`, `LevelFile`, `AnimatorFile` |
| `int Selected` | 4 | `Dropdown`, `UiWheel`, `UiTabs`, `SongSelectState` |
| `float Radius` | 4 | `ShapeGlow`, `TextGlow`, `NotePop`, `AnimatorWeightPaint` |
| `Vector2 Anchor` | 4 | `UiAnchor`, `Chord`, `AnimatorKeyMenu`, `AnimatorBoneMenu` |
| `long TimeMs` | 4 | `Playhead`, `Note`, `Chord`, `Pad` |
| `int Attachment` | 4 | `AnimatorMeshPick`, `AnimatorWeightPaint`, `AnimatorHover`, `AnimatorSelection` (all indexes into a C# list; rule 3) |

### 3.3 Families of idea-centric components

These are the shapes rules 3 to 6 remove. Counts are approximate; classify each
member before touching it.

- **Session singletons (animator, ~30).** One session entity carries
  `AnimatorModes`, `AnimatorSelection`, `AnimatorHover`, `AnimatorDrag`,
  `AnimatorKeyMenu`, `AnimatorBoneMenu`, five dialog `Open` components,
  `AnimatorMeshPick`, `AnimatorWeightPaint`, `AnimatorSequence`,
  `AnimatorAttackCycle`, `AnimatorKeyTryout`, `AnimatorFlow`, `AnimatorPlants`,
  `AnimatorSnapGuides`, `AnimatorTracks`, `AnimatorStatus`, `AnimatorFile`,
  `AnimatorDocument`, `AnimatorSessionState`, `AnimatorUi`, …. Each panel,
  menu, tool and mode should be its own entity with shared atoms (`Open` tag,
  `Anchor`, `Active` tag), found through the entity tree or a role.
- **Handle bags (~20).** `AnimatorUi`, `EditorHud`, `EditorDialogs`, `ScoreState`,
  `JudgementCounter`, `SongSelectState`, `SongTimingView`, `SceneConfirmation`,
  `BenchView`, `WarningPopup`, `ComboHud`, `TimingIndicator`, `EditorViewBar`,
  `EditorLeftMenu`, `UiColorPicker`, `UiStepper`, and the handle parts of
  `UiSlider`, `UiValueField`, `UiTabs`, `PlaySession`, `PlayOffset`, `ErrorBar`.
- **"Which one am I" kind components (~15).** `EditorReadout`, `EditorPanel`,
  `EditorField`, `EditorDropdownControl`, `EditorColorRow`, `EditorDialog`,
  `EditorUiRequest`, `SceneBackground`, `PlayfieldMode`, `SongTimingField`,
  `ParticleEditorField`, `ParticleEditorSlider`, `XifEditorField`,
  `AnimatorValueBinding`, `ModeToggle`. Each holds an enum saying what the
  entity is or edits. Usually that is a link to the edited entity (rule 3) plus a
  typed signal (rule 4); where a real "kind" remains it is a tag. **Open question
  (§8)**: one shared indexed `Role` atom versus tags.
- **Intent carriers (5).** `DropdownChanged` (set for one frame),
  `UiTreeChanged` (the consumer removes it), `EditorUiRequest`, `WarningRequest`,
  and the `Pending`/`Requested` fields inside others (`SceneState.Pending`,
  `EditorDialog.OpenRequested`, `AnimatorFile.CloseRequested`,
  `EditorViewNavigation.FitRequested`, `UiColorDrop.Pending`). All become signals.
- **Envelopes riding on components.** `UiCommand` (scope, id, `Arg`, `Text`,
  `Owner`) carries `ToolAction` (animator) and the editor/scene button enums.
  `EditorCommand` (Logic/Editor/Commands) is a non-ECS bus of seven optional
  slots. Both go: pressing a control emits the typed signal directly.
- **Host interfaces in components (~7).** `TimelineHost`, `EditorShortcutHost`,
  `XifEditor.Host`, `ParticleEditor.Host`, `EasePicker.Host`, `TextEditor.Host`,
  `AnimatorDocument.Files`. Rule 6.
- **C# object stores.** `AnimatorDocument.Package` (the whole rig),
  `Puppet.Package`, `CharacterRig.Runtime`, `LevelGroups.Groups`,
  `ReplayRecorder` lists, `SongSelectState` lists. The rig ones go under rule 8.
  The others stay until each has its own decision; flag them, don't move them
  on the way past.

### 3.4 A good example already in the repo

`Tools/Labs/Experiments/EcsRender` (`Placed`, `Depth`, `Tint`, `Shape`, `Label`,
`Drift`) is data-centric: short names for data, reused by everything drawn.
That is the target style.

## 4. Friflo features to use (verified against 4.0.0-preview.2)

Checked in the package's XML docs on 2026-11-23. **Verify any API you use
before writing it**: twice that day a plausible API turned out not to exist.

| Need | Feature | Notes |
|---|---|---|
| Kind of thing, on/off state | `ITag`, `entity.Tags.Has<T>()`, `Filter.AllTags(Tags.Get<T>())` | 255 tags, currently 39 used: plenty of room |
| Find by name (CLI, files, UI tree keys) | `IIndexedComponent<string>` + `store.ComponentIndex<TComp, string>()[value]` | O(1). Already used for `Layer` (int) |
| "This is about that entity" | `ILinkComponent` (`GetIndexedValue()` returns the target) | Reverse lookup: `target.GetIncomingLinks<T>()` returns `EntityLink<T>`s, O(1). Already used by `Dropdown`/`DropdownOption` |
| Many links from one entity | `ILinkRelation` + `AddRelation` / `GetRelations` / `RemoveRelation` | e.g. a mesh part's weighted bones |
| Hierarchy (skeleton, panels) | entity tree: `AddChild`, `ChildEntities`, `Parent` (`TreeNode`) | Friflo's own; costs no slot of ours |
| Intents | `entity.EmitSignal(in T)`, `entity.AddSignalHandler<T>(Action<Signal<T>>)`, `Signal<T>.Entity` / `.Event` | Not a component: no slot cost. About 10 ns per handler. Handlers are per entity |
| React to tag changes now | `store.OnTagsChanged` (`TagsChanged.AddedTags`, `.RemovedTags`, `.Entity`) | e.g. "anything that becomes a Bone gets bone behaviour" |
| React to component add/update/remove now | `entity.OnComponentChanged` (`ComponentChanged.Action` Add/Update/Remove, `.Component<T>()`, `.OldComponent<T>()`); `store.OnComponentAdded/Removed` | `AddComponent` on an existing component is an **Update**. Mutating through `ref` fires nothing. The UI already reports toggles this way (`UiToggles.OnToggled`) |
| "What changed since my last update" in a system | `Query.EventFilter.TagAdded<T>()` / `TagRemoved<T>()` / `ComponentAdded<T>()` / `ComponentRemoved<T>()`, then `Query.HasEvent(id)` | Needs `store.EventRecorder.Enabled = true` (set once in Wiring) and `EventRecorder.ClearEvents()` each frame. **Component updates are not recorded**, only add and remove |
| Structural change from a handler | `store.GetCommandBuffer()` (`ReuseBuffer = true`) kept by the owning system, `Playback()` in its `OnUpdate` | A system's own `CommandBuffer` property belongs to its update; this Friflo build has no `OnAddStore` hook, so take the store in the constructor |
| Name a singleton | prefer an indexed component or a tag | The `use-native-ecs-events` memory: find entities by indexed component, not `UniqueEntity` |

House rules that still apply (from memory and `agents.md`):
- React through native events or queries. **No delegate fields of our own, no
  marker-tag-plus-system tricks, no one-frame tags or components.** These rules
  stop code going around the ECS; Friflo's own event mechanics (signals,
  `OnComponentChanged`, `OnTagsChanged`, `EventRecorder`) are the ECS and are fine (§8).
- Loops follow `agents.md` "ECS in ten points": `IEach`/`IEachEntity` structs,
  not `ForEachEntity` lambdas or `query.Entities` + `GetComponent` (the mockup
  in §5 is shorthand, not a style to copy). ECS reference: `Docs/ecs/README.md`
  and `python3 Docs/ecs/lookup.py <Type>`.
- Layering: Drawers see Core/UI/Slang/Model only, Logic never sees
  Screens/Drawers/Wiring, Screens never edit state themselves (they emit
  intents, which Logic handles). Shared atoms live as low as their users allow:
  UI-library atoms in `UI/` (the UI must stay separable and never reference the
  game), game atoms in `Model/`.
- New or moved files take the namespace of their folder.
- Mid-prototype: build the game projects only. Don't run, fix or prune tests.

## 5. Mockup: rig tree panel ↔ bone logic

Not compiled. It shows the shape, written to the rules above. An earlier draft
broke rule 2 itself (`BonePose`, `BoneWorld` are idea names), corrected here.

```csharp
// ── Model: shared atoms (names say what the data is) ──
public struct Name(string value) : IIndexedComponent<string>     // files, CLI, UI keys find things by it
{ public string Value = value; public readonly string GetIndexedValue() => Value; }
public struct LocalTransform : IComponent { public Transform2D Value; }   // also: UI poses, puppet parts, decos
public struct WorldTransform : IComponent { public Transform2D Value; }
public struct Bone : ITag;
public struct Selected : ITag;                                    // selection IS tags; no index lists
public struct Subject(Entity target) : ILinkComponent             // a control (row, field, button) is about this entity
{ public Entity Target = target; public readonly Entity GetIndexedValue() => Target; }

// Intents: typed signal payloads, no component slot each
public readonly record struct Pick(bool Additive);
public readonly record struct SetRotation(float Radians);

// ── Logic: owns what happens to bones; never hears from a button directly ──
public sealed class BoneLogicSystem : QuerySystem
{
    private readonly CommandBuffer _changes;

    public BoneLogicSystem(EntityStore store)
    {
        _changes = store.GetCommandBuffer();
        _changes.ReuseBuffer = true;
        // Whatever becomes a Bone (file load in Storage, ADD BONE, undo) gets bone behaviour.
        store.OnTagsChanged += changed =>
        {
            if (!changed.AddedTags.Has<Bone>()) return;
            changed.Entity.AddSignalHandler<Pick>(s => Select(s.Entity, s.Event.Additive));
            changed.Entity.AddSignalHandler<SetRotation>(s => Rotate(s.Entity, s.Event.Radians));
        };
    }

    private void Select(Entity bone, bool additive)
    {
        if (!additive)
            foreach (Entity was in bone.Store.Query().AllTags(Tags.Get<Selected>()).Entities)
                _changes.RemoveTag<Selected>(was.Id);
        _changes.AddTag<Selected>(bone.Id);
    }

    private void Rotate(Entity bone, float radians)
    {
        Transform2D local = bone.GetComponent<LocalTransform>().Value;
        _changes.AddComponent(bone.Id, new LocalTransform { Value = local with { Rotation = radians } }); // an Update event
    }

    protected override void OnUpdate() => _changes.Playback();
}

// ── Screens: shows state, emits intents ──
public sealed class RigTreePanelSystem : QuerySystem<Name>
{
    public RigTreePanelSystem()
    {
        Filter.AllTags(Tags.Get<Bone>());
        Query.EventFilter.TagAdded<Selected>();                   // EventRecorder enabled in Wiring
        Query.EventFilter.TagRemoved<Selected>();
    }

    public static Entity BuildRow(UiNode list, Entity bone)
    {
        Entity row = list.TreeRow(bone.GetComponent<Name>().Value).Entity;
        row.AddComponent(new Subject(bone));
        // UI library reports the press as a native component update (as Toggle does today).
        row.OnPressed(press => row.GetComponent<Subject>().Target.EmitSignal(new Pick(press.Shift)));
        return row;
    }

    protected override void OnUpdate()
    {
        foreach (Entity bone in Query.Entities)
        {
            if (!Query.HasEvent(bone.Id)) continue;               // only bones whose selection changed
            foreach (EntityLink<Subject> link in bone.GetIncomingLinks<Subject>())
                UiTreeRows.SetHighlighted(link.Entity, bone.Tags.Has<Selected>());
        }
    }
}

// ── the same state, read by others ──
// Drawers: Query<WorldTransform>().AllTags(Bone): lit when Tags.Has<Selected>(). Never told anything.
// Tools (CLI "--select=hand_l"):
//   foreach (Entity bone in store.ComponentIndex<Name, string>()["hand_l"]) bone.EmitSignal(new Pick(false));
```

What disappears in this picture: `ToolAction`, the 127-case `AnimatorCommands.Run`
switch, `AnimatorSelection`'s index lists, `AnimatorRigTree`'s `"part:3"` keys,
`UiTreeChanged`, and every bone index passed between systems.

`UiPress`/`OnPressed` and a typed `OnValueChanged<T>` are new UI-library pieces
(same pattern as `UiToggles.OnToggled`). `UiTreeRows.SetHighlighted` is illustrative.

## 6. What 2026-11-23 already did (interim, keep or replace)

- **Animator split into the role projects** (`Model/Animator`, `Logic/Animator`,
  `Drawers/Animator`, `Screens/Animator`; the exe in `Tools/Animator` is wiring
  only and no longer references `Game`). See the `animator-role-split` memory.
- **Typed `ToolAction`** (`Model/Animator/ToolActions.cs`): `(Kind, int Arg, string Name)`
  with `With<T>(kind, enum)` / `As<T>()`, `AnimatorTargetVerb`, `AnimatorLayerRow.Key`,
  `AnimatorRigRow` in `Screens/Animator/AnimatorRigTree.cs`, and
  `Tools/Animator/AnimatorLaunch.cs` (CLI parsed once). This is still an
  envelope, a better one. It is the bridge until signals replace it (§7, phase 2).
- **Repo state at hand-over:** all of the above is staged and **not committed**.
  `Tests/Animation/Tool` does not compile against it (old namespaces and the old
  `Argument` API); left alone under the prototyping rule. A throwaway git worktree
  of the previous HEAD is registered in the session scratchpad
  (`git worktree list`); `git worktree remove --force <path>` clears it.
- Known older bug, not caused by this: the animator's `--paths=` flag sets motion
  paths on, but the PATHS toggle doesn't light and no paths draw (same at the
  previous HEAD).

## 7. Migration plan

Each phase builds and runs on its own. Phase 0 needs the user before anything moves.

**Phase 0: vocabulary.** Done: §10 (atoms and the per-component mapping),
principle in `agents.md`. Names are the agent's call (§8).

**Phase 1: mechanical merges, no behaviour change.**
- Bool-only and empty components → tags (the 5 dialog `Open`s → `Open`;
  `EditorMetronome`, `CharacterFacing`, `UiBackground`, `EditorNoteBarPanel`,
  `EditorSongLabel`, `SpectrumEnergy`).
- True duplicates → one atom (`ShapeGlow` + `TextGlow` → `Glow`; colour fields
  that mean "this thing's tint" → one `Tint`; `Path`).
- Check `Core.Position` (our `Vector2`) against Friflo's own `Position` (3D,
  already counted in the 8 built-ins) and decide which wins.
- Expected: the component count drops by roughly 15 to 25.

**Phase 2: intents → signals.**
- UI library first: press and value-change reporting as native events plus
  `EmitSignal` to the control's `Subject`; `DropdownChanged`, `UiTreeChanged`
  gone. The UI stays game-free: it emits UI-level signals; game code defines its own.
- Then `EditorUiRequest`, `WarningRequest`, the `*Requested`/`Pending` fields.
- Then `ToolAction` + `AnimatorCommands.Run` and `EditorCommand` + `EditorCommandBus`:
  one payload record per intent, handled by the owning Logic system.
- Then host interfaces (rule 6), one at a time; `TimelineHost` is the biggest.
- `UiCommand` goes last, once nothing reads it (shortcut tables, tooltips, lit
  state must read state or be set at build time first).

**Phase 3: handle bags → entity tree.**
Panels find their parts through children, role tags or incoming links. Start
with `AnimatorUi`; the session singletons (§3.3) split into one entity per panel,
menu, tool and mode on the shared atoms from phase 1.

**Phase 4: the rig as entities (rule 8).**
- Bones, parts, chains, cloth, clips, keys and limb targets become entities built from
  shared atoms (`Name`, `LocalTransform`/`WorldTransform`, kind tags, links for
  bone ownership and weights, entity tree for the skeleton).
- Serialisation/Storage read and write `.xif` to and from entities.
- `AnimatorRuntime`, `RigClothSimulation`, `RigChainSimulation` and the game's `Puppet`
  read entities. Fit with the Xif modules direction (`Docs/xif-modules.md`).
- **Undo** must move from copying `RigPackage` to entity snapshots or diffs.
  This is the biggest design item and needs its own proposal before code.
- Selection, hover, mesh pick and weight paint become tags and links.

**Phase 5: gameplay onto shared atoms.**
`Note`, `Chord`, `Pad`, `Deco`, `DashStop`, `Draft`, `HitTrail`, `NotePop`,
`ParticleBurst` onto `Position`, `TimeMs`, `Tint`, … plus role tags, in line
with the `generic-gameplay-roles` memory.

## 8. Decisions (user, 2026-11-23)

1. **Names:** agents choose; they must be sensible data names. No sign-off per name.
2. **Signals are fine.** The "no delegate fields" rule exists to stop code going
   *around* the ECS, not to ban the ECS's own event mechanics. Signal handlers,
   `OnTagsChanged`, `OnComponentChanged` and `EventRecorder` are all allowed.
3. **EventRecorder can be used.**
4. **Undo** works the way undo works in an ECS: record entity changes (components
   and tags added, updated and removed, entities created and deleted) per
   step, and apply them backwards. It needs a design note at phase 4, not a debate.
5. **Position:** either works; keep `Core.Position` (2D, already used everywhere)
   and don't add Friflo's 3D one to new code.
6. **Order:** start with the shared vocabulary (§10), then the phases in §7.
7. **Enums:** see §11.

## 9. How to work on this

- Re-measure before each phase (§3 method, or the DevTools ATLAS). The numbers
  here are a snapshot.
- Classify before merging: same shape is a prompt, same meaning is the test.
- One phase per change set; build `Game/Host`, `Tools/Animator`, `Tools/Labs`,
  `Tools/DevTools` (not tests); one animator screenshot (`dotnet run --project
  Tools/Animator -- --shot=<png>`) for animator-facing phases.
- Never grow the pile: a new component must name a data shape and must not
  duplicate an existing atom's meaning. If you are about to name a component
  after a feature, stop and find the atom.


## 10. Shared vocabulary (phase 0)

Atoms live as low as their users allow. Ones the UI library uses go in **Core**
(level 1, below UI, so the UI stays separable and game-free). Game-only ones go
in **Model**. Names follow §2: what the data is.

### 10.1 Atoms

Tags (state and kind):

| Atom | Meaning | Replaces / absorbs |
|---|---|---|
| `Open` | a dialog, menu, flyout or dropdown list is showing | 5 animator dialog components, `Open` fields in `AnimatorKeyMenu`, `AnimatorBoneMenu`, `Dropdown`, `XifEditor`, `EditorToolGroup` |
| `On` | a switch is on | `Toggle.Value`, `EditorMetronome.Enabled`, `PlayerFriction.On`, `AnimatorPreviewMode.On`, the 12 bools in `AnimatorModes` (each mode its own tag on the session or document) |
| `Active` | a tool or run is in progress | `Active` in `UiMarqueeBox`, `AnimatorMeshPick`, `AnimatorWeightPaint`, `AnimatorAttackCycle`, `AnimatorKeyTryout`, `AnimatorSequence` |
| `Selected` | chosen by the user | `AnimatorSelection`, `NoteSelection`, `Dropdown.Selected`, `UiTabs.Selected`, `UiTree.Selected`/`MultiSelected`, `SongSelectState.Selected`, `EditorGroupEdit.Picked` (each on the chosen entities) |
| `Hovered` (exists, Core) | under the pointer | `AnimatorHover` (once bones and parts are entities) |
| `Focused` (exists, UI; move to Core) | receives keyboard input | `TextFieldFocused`, `TextEditorFocused` (check that only one thing holds focus at a time) |
| `Expanded` | a collapsible node is open | `UiCollapsible.Expanded`, `AnimatorTracks.Expanded` |
| `Unsaved` | the document differs from its file | `AnimatorFile.Dirty` (not Core's `Dirty`, which means "redraw me") |
| `FacingLeft` | mirrored horizontally | `CharacterFacing`, `Puppet.FacingLeft` |
| empty components | should be tags | `UiBackground`, `EditorNoteBarPanel`, `EditorSongLabel`, `SpectrumEnergy` |

Components (one shape each):

| Atom | Shape | Replaces / absorbs |
|---|---|---|
| `Position` (exists, Core) | `Vector2` | `DashStop`, and the position fields of `Note`, `Chord.Anchor`, `Pad`, `Deco`, `Draft`, `HitTrail`, `NotePop`, `ParticleBurst`, `CurveBody`, `EditorPlacementGhost.Screen` |
| `Size` | `Vector2` | `Player`, `Playfield` (as float), `Shape.Size` (Labs) |
| `Velocity` | `Vector2` | `CurveBody.Velocity`, `CharacterBump.LastVelocity` (check) |
| `Anchor` | `Vector2`, where a popup opens | `AnimatorKeyMenu.Anchor`, `AnimatorBoneMenu.Anchor` (not `UiAnchor`, which is layout) |
| `Home` | `Vector2`, where a floating panel rests | `EditorPanel.Home`, `ParticleEditor.Home`, `XifEditor.Home` |
| `TimeMs` | `long`, song time | `Note`, `Chord`, `Pad` (and `Playhead.TimeMs` stays inside the clock) |
| `Lifetime` | `float Age, float Duration` | `HitTrail`, `NotePop`, `CharacterBump.Age` (check) |
| `Index` | `int`, position in an ordered list | `ChartIndex`, `SongSelectTile`, `AnimatorTargetCaption`, `UiValueSlot.Index`, `EditorToolGroup.Index` |
| `FilePath` | `string` | `CharacterRigSource`, `LevelFile`, `AnimatorFile.Path`, `WaveformPreview.Path`, `EditorRecoveryOffer.Path` |
| `Name` | indexed `string` | rig and level things found by name (CLI, files); `UiTreeNode.Id` |
| `Glow` | `Color, float Radius, float Intensity` | `ShapeGlow`, `TextGlow` |
| `Outline` | `Color, float Width` | `TextOutline` (int size → float), `UiBorder` |
| `Opacity` | `float` | `LayerOpacity` |
| `Mass` | `float` | `Character` |
| `Scroll` | `float`, first row or offset | `ScrollView`, `AnimatorTracks.FirstRow`, `EditorGroupEdit.FirstRow` |

Links and selectors (the enum replacements, §11):

| Atom | Shape | Replaces / absorbs |
|---|---|---|
| `Subject` | `ILinkComponent` → the entity a control acts on or shows | `AnimatorValueBinding` targets, `AnimatorKeyMenu`/`AnimatorBoneMenu` targets, `Renaming`/`Reparenting`, weight-paint part and bone |
| `Owner` | `ILinkComponent` → the widget a part belongs to when it is not its tree parent | `DropdownOption.Owner`, `UiValueSlot.Field`, `ParticleEditorField.Editor`, `ParticleEditorSlider.Editor`, `XifEditorField.Editor` |
| `Switches` | `Tags` value: which tags this control turns on and off on its `Subject` | `ModeToggle` |
| `Shows` | a component type reference (schema index): which atom of its `Subject` a field or readout shows and edits | `EditorReadout`, `EditorField`, `SongTimingField`, `EditorColorRow`, `ParticleEditorField.Kind`, `XifEditorField.Kind`, `AnimatorValueBinding.Kind` |

### 10.2 Same shape, different meaning: deliberately kept apart

`Drawable` (an item's draw order) and `Layer` (a layer entity's order);
`FillColor` (base fill) and `SelfModulate` (multiplied over it, see the
`self-modulate-layering` memory); `UiHidden` (authored, leaves layout) and
`CanvasHidden` (not drawn, still laid out); `UiRotation` and `UiPose` (a pose
on top of the rotation); `UiAnchor` (layout) and `Anchor` (popup point);
`UiImage` (texture handle) and `VisualItem` (canvas item handle);
`UiTreeOverlaySelected`/`Hovered` (F4 inspector state, which sits on entities
that also carry the ordinary `Hovered`); `Playfield` w/h and `AnimatorHover`.

### 10.3 Already data-centric, stays

Core state tags (`Dirty`, `Uninitialised`, `TakesInput`, `Hovered`, `Pressed`,
`LayoutIgnore`, `UiHidden`, `UiDisabled`, `DebugUi`); UI drawing and layout
(`Rectangle`, `Text`, `TextStyle`, `TextAlignment`, `TextMono`, `TextFit`,
`TextWrap`, `FillColor`, `SelfModulate`, `LinearGradient`, `UiShape`,
`UiImage`, `UiMaterial`, `UiMaterialBinding`, `UiContainer`, `UiFlex`,
`UiAnchor`, `UiChamfer`, `UiMeter`, `UiList`, `UiBarGraph`, `UiWheel`,
`UiThemeScope`, `UiTweens`, `VisualItem`, `Drawable`, `Layer`, `UiOverlay`,
`UiOverlayItem`, `UiRaised`, `UiSliderFill`, `UiColorSwatch`,
`UiTreeDisclosure`, `UiTreeLabel`, `DropdownListPanel`, `DropdownShowsPick`,
`ButtonVisual`); clocks and views (`TimelineView`, `Playhead`, `PlayfieldView`,
`PlayCamera`, `TrackProgress`); game data (`LevelInfo`, `PhysicsBody`,
`ShaderParam`, `HudLayout`, `CharacterMotion`, `CharacterAttack`,
`CharacterBump`, `PlayerSmoothing`, `PlayerTuck`, `BodyPunch`, `BodyTrace`,
`SpeedGraph`, `ReplayRecorder`, `ReplayPlayer`, `TrackProgress`); game state
tags (`EditorItem`, `EditorSaved`, `EditorBaselineStale`, `NoteMissed`,
`PlayPaused`, `SongSelectDragging`, `CharacterRigVisible`); Labs `EcsRender`
(lab-local example). The Core `*Dirty`/`Moved` marker tags are candidates for
`EventRecorder` later, not part of this work.

### 10.4 Dissolves in a later phase

- **Signals (phase 2):** `DropdownChanged`, `UiTreeChanged`, `EditorUiRequest`,
  `WarningRequest`, `UiColorDrop`, `AnimatorStatus` (status messages become a
  toast signal), `*Requested`/`Pending` fields, `UiCommand` (last), host
  interfaces (`TimelineHost`, `EditorShortcutHost`, `EasePicker.Host`,
  `ParticleEditor.Host`, `XifEditor.Host`, `TextEditor.Host`, `AnimatorDocument.Files`).
- **Entity tree and roles (phase 3):** handle bags (`AnimatorUi`, `EditorHud`,
  `EditorDialogs`, `ScoreState`, `JudgementCounter`, `SongSelectState`,
  `SongTimingView`, `SceneConfirmation`, `BenchView`, `WarningPopup`, `ComboHud`,
  `TimingIndicator`, `EditorViewBar`, `EditorLeftMenu`, `UiColorPicker`,
  `UiStepper`, handle parts of `UiSlider`, `UiValueField`, `UiTabs`,
  `PlaySession`, `PlayOffset`, `ErrorBar`, `UiConnector.From/To` as links); the
  animator session singletons split into one entity per panel, menu, tool and mode.
- **Rig as entities (phase 4):** `AnimatorDocument`, `AnimatorSessionState`
  (history, clipboards), `AnimatorChains`, `AnimatorCloths`, `AnimatorPlants`
  (→ a `Planted` tag), `AnimatorFlow` target, `CharacterRig`, `Puppet`.
- **Variant enums to tags (phase 5 for gameplay):** `Note.Kind`,
  `PlayfieldMode`, `SceneBackground`, `EditorPlacement.Mode`, `EditorPanel.Kind`,
  `EditorDialog.Kind`, `EditorDropdownControl`.
- **Unsure, decide when there:** `TextField.Value` versus `Text` (a field entity
  may carry both), `EditorSidebar` (a region, possibly `Rectangle` plus a tag),
  `AnimatorPreviewView` and `PlayfieldView` as one 2D view atom, `AnimatorDrag`,
  `AnimatorSnapGuides`, `LevelGroups`.

### 10.5 Phase 1 work list (mechanical, no behaviour change)

1. Empty components → tags: `UiBackground`, `EditorNoteBarPanel`, `EditorSongLabel`, `SpectrumEnergy`.
2. `ShapeGlow` + `TextGlow` → `Glow`; `TextOutline` + `UiBorder` → `Outline`.
3. `TextFieldFocused` + `TextEditorFocused` → `Focused`, moved to Core.
4. The five animator dialog components → an `Open` tag on each dialog's own
   root entity (they already exist in the layout).
5. `CharacterFacing` and `Puppet.FacingLeft` → `FacingLeft`.
6. `CharacterRigSource`, `LevelFile` → `FilePath`; `ChartIndex`, `SongSelectTile`,
   `AnimatorTargetCaption` → `Index`.
7. `Character` → `Mass`, `LayerOpacity` → `Opacity`, `ScrollView` → `Scroll`
   (renames: no slot saved, done while the files are open anyway).
8. `AnimatorModes` → one tag per mode, `ModeToggle` → `Switches` + `Subject`
   (this brings in the first selector atoms; do it last in the phase).

Re-measure after: the component count should fall by roughly 15.

## 11. Enums

An enum on a component is one of four different things. Each has its own ECS answer.

**1. A value (keep the enum).** The data itself is one of a few options: a key's
ease (`EasePreset`), a fade curve (`RigFadeCurve`), text alignment
(`TextAlign`). Nothing switches on it to decide *what the entity is*; it is
read as data, like a float. These stay enum fields.

**2. A variant: what kind of thing this is (→ one tag per variant).**
`Note.Kind`, `PlayfieldMode.Kind`, `SceneBackground.Kind`, `EditorPanel.Kind`.
Systems `switch` on it to pick behaviour. In an ECS the kind is the archetype:
one tag per variant, and each behaviour is a system (or query) that filters on
its tag. The `switch` disappears, queries skip what they don't handle, and a
new kind is a new tag plus its systems, not a new `case` everywhere. Tags are
cheap (39 of 255 used). Where one entity must be exactly one of a set, a small
helper swaps the tag (`entity.RemoveTags(all); entity.AddTag<T>()`), still with
native tag events.

**3. A verb: what to do (→ one signal payload type per verb).**
`ToolActionKind`, `EditorCommandType`, `EditorButtonKind`. The enum exists only
so one envelope can carry many verbs through one channel. With signals each
verb is its own small record (`Pick`, `SetRotation`, `AddKey`) carrying exactly
its data, sent to its subject, handled by its owner. The enum and its `switch`
are gone; the compiler checks payloads, and handlers are found by type.

**4. A selector: which part of another thing (→ a schema reference, not an enum).**
`ModeToggle.Mode` ("which mode does this button flip"),
`EditorField.Kind` / `SongTimingField.Kind` / `AnimatorValueBinding.Kind`
("which value does this field edit"). These enums mirror names that already
exist in the ECS schema: tag types and component types. Point at the schema
directly:

- **Which tags:** Friflo's `Tags` is a plain value type (a bit set). Store it:
  `Switches { Tags Tags }` plus a `Subject` link. The toggle system does
  `subject.Tags.HasAll(in switches.Tags)` to light it and
  `subject.AddTags(in t)` / `RemoveTags(in t)` to flip it. One atom serves every
  toggle of every editor. The mode enum and the `AnimatorModes` struct go away.
- **Which component:** once data is one atom per value (§10), "which value" is
  "which component type". `Shows` stores the component's schema identity
  (`store.GetEntitySchema().GetComponentType<T>()`, its `StructIndex`), and a field reads
  and writes through it on its `Subject`, with typed fast paths for the common
  shapes (float, `Vector2`, `Color`, string). One atom replaces every per-panel
  field-kind enum.

So there is **no shared `Role` atom.** Variants become tags, verbs become signal
types, selectors become schema references, and value enums stay as data.

[generic gameplay roles]: see the `generic-gameplay-roles` memory: objects are role components plus data and tags.

## 12. Progress note (2026-11-23, paused after phase 1)

**Phase 1 done**, uncommitted, all game projects build (Host, Animator, Labs,
DevTools, Showcase). Components 190 → 175, tags 38 → 58 (tags are cheap; the
animator process was 183/255 components before, so about 168 now).

Done:
- Empty components → tags: `UiBackground`, `EditorNoteBarPanel`, `EditorSongLabel`, `SpectrumEnergy`.
- `ShapeGlow` + `TextGlow` → `Glow` (UI).
- Five animator dialog components → Core `Open` tag on each dialog's root
  entity (`AnimatorUi.NewClipDialog`, `.InfoPanel`, `.ShortcutsPanel`,
  `.ClipLengthPanel`, `.EasePicker`); helpers `IsOpen`/`SetOpen`/`ToggleOpen`
  in `Core/Tags.cs`; `AnimatorUi.Of(session)` reads the handles safely.
- `CharacterFacing` (dead) deleted; `Puppet.FacingLeft` and `PuppetSystem`'s
  private `_facingLeft` field → `FacingLeft` tag on the player entity.
- `CharacterRigSource` and `Character` (both dead) deleted.
- `LevelFile` → Core `FilePath` (`.Value`).
- `ChartIndex`, `SongSelectTile`, `AnimatorTargetCaption` → Core `ListIndex`
  (`System.Index` would clash); the last two kept as role tags because queries find them by role.
- `LayerOpacity` → `Opacity`; `ScrollView` → `ScrollOffset` (`.Value`; `Scroll` clashes with a UI tree class).
- `AnimatorModes` component → 12 mode tags on the session (`RestPose`,
  `AutoKey`, `OnionSkin`, `PlaceParts`, `IkDrag`, `PinChildren`, `ShowKites`,
  `ShowHandles`, `AlignJoints`, `ShowMotionPaths`, `PathWindow`, `FullKeys`).
  `AnimatorModes` survives only as a read-only snapshot (`AnimatorModes.Of(session)`),
  and `AnimatorModeTags` maps the interim `AnimatorMode` enum to tags (it goes with ToolAction in phase 2).
- `ModeToggle` → Core `Switches { Tags, Synced }` + Core `Subject` link
  (`Core/Links.cs`); `AnimatorModeSystem` links a switch to the session on first sight.
- Core `TagSwitch.SetTag<T>(bool)` / `SetTags(in Tags, bool)`.

Deliberately not done (decided while there):
- `TextOutline` + `UiBorder` → `Outline`: **not merged.** On a button the box
  border and the text outline are two parts of the same entity, so the same
  shape has different meanings (moved to §10.2 in spirit).
- `TextFieldFocused` + `TextEditorFocused` → one focus tag: deferred. It saves a
  tag, not a component, and the removal paths need auditing first. Core
  `Focused` is the focus ring, which is a different meaning.

Lessons for whoever continues:
- **Turning a bool into a tag moves the entity to another archetype.** A
  `ref X = ref entity.GetComponent<X>()` taken before the tag change is stale
  after it. `AnimatorPreviewModeSystem` was rewritten for this; I audited the
  rest of the animator for refs held across mode, workspace, file or command
  calls and found none on the session. Repeat that audit for every bool-to-tag conversion.
- Structural changes inside `foreach (… in Query.Entities)` over the same
  archetype are unsafe. The animator's session loops use `ToEntityList()` or
  collect then act, which is why immediate tag writes are fine there.
- Bulk renames must skip test folders (the prototyping rule). One UI test file
  was edited by accident and reverted. Consequence: `UI/Tests` (FpsCounter
  tests still say `TextGlow`) and the game `Tests` no longer compile. Leave
  them until the user lifts the rule.
- The old `--paths` bug is still there (PATHS toggle unlit and no paths drawn,
  though the `ShowMotionPaths` tag is set). It predates this work; not investigated.

**Next (phase 2, §7):** intents → signals. Start in the UI library
(`DropdownChanged`, `UiTreeChanged` → signals on the control's `Subject`), then
`EditorUiRequest`, `WarningRequest`, the `*Requested`/`Pending` fields, then
`ToolAction` + `AnimatorCommands.Run` and `EditorCommand` + `EditorCommandBus`,
then host interfaces, with `UiCommand` last. Re-measure first (§3 method).

## 13. Progress note (2026-11-23, phase 2 started)

Phase 2 steps 1–2 done, uncommitted, all game projects build; animator and
editor screenshots unchanged. Clicks were not exercised (no scripted click
for the tree, dropdowns or editor buttons): check by hand.

Signals are plain `readonly struct`s (not components, no slot cost), emitted
on the entity they are about and heard with `AddSignalHandler`:

| Was (component) | Now (signal) | Emitted on | Heard by |
|---|---|---|---|
| `DropdownChanged` | `DropdownPicked { Value }` (UI) | the dropdown button | `UiDropdowns.OnPicked`: particle lab, shader tool tuning, Xif editor + LAYER (`XifEditorSystem.AddLayer`, wired in `XifEditorPanel`) |
| `UiTreeChanged` | `TreeNodeSelected { Id }` (UI) | the tree | `UiTreeControl.OnSelected`: animator rig tree (`AnimatorHierarchySystem.TreePicked`) |
| `WarningRequest` | `WarningRaised { Message }` (Model) | the editor | `EditorWarningPopup` shows it; `WarningPopupSystem` only dismisses |
| `EditorUiRequest` | `EditorAsked { Kind }` (Model) | the editor | metadata panel toggles at once (`EditorMetadataSystem.Toggle`); `EditorOverlay` queues its kinds for `Tick` |

Rules used, worth keeping:
- **Emit after the loop.** An emitter inside `EachEntity`/`foreach` collects
  what it will emit and emits after the loop (`UiDropdownSystem`,
  `EditorButtonSystem`, `EditorAutosaveSystem`), because handlers may change structure.
- **Scene swaps wait.** The overlay's handler only queues: leaving and
  previewing swap the scene, which must not happen inside a system update.
  The queue is the listener's own state, not a component.
- `EditorAsked` still carries the `EditorButtonKind` enum; it splits into
  one payload per intent with `EditorCommand` (step 4).
- The colour buttons on LEVEL INFO (`BackgroundColor`, `OutsideColor`,
  `FrameColor` and their resets) asked for UI work nobody did, before and
  after; under the old component they also blocked autosave's quiet save
  until something else cleared the request.
- Tests still name the old types (`SceneActionRegistryTests`,
  `AnimatorHierarchyTests`); left per the prototyping rule.

### 13.1 Step 3: `*Requested` / `Pending` fields (2026-11-23)

Done, builds; editor, animator and autoplay runs come up. Not exercised by
hand: Enter-to-skip, autoplay's skip, F-fit, colour drag-and-drop, CLOSE EDITOR.

| Was (field) | Now (signal) | Emitted on | Heard by |
|---|---|---|---|
| `EditorViewNavigation.FitRequested` | `ViewFitAsked` | the editor | `EditorViewSystem.Listen` (wired in `EditorScene`) fits the view at once |
| `EditorDialog.OpenRequested` (+ unused `Arg`) | `DialogOpenAsked` | the dialog root | `EditorDialogSystem` fills and shows it; `IsOpen`/`AnyOpen` read visibility only |
| `PlaySession.SkipRequested` | `SkipAsked` | the play session | `PlaySkipSystem.Asked` skips at once if a player could press SKIP |
| `AnimatorFile.CloseRequested` | `CloseAsked` | the animator session | `AnimatorTool` quits on its next tick |
| `UiColorDrop { Pending, Color }` | `UiColorDrop` is now a tag; `ColorDropped { Color }` | the drop target | `UiColorDrops.OnColorDropped` → `UiColorFields.Drop` (both halves of a colour field) |

Kept on purpose:
- `SceneState.Pending` is the scene being moved to, which is state the
  transition reads over several frames, not a one-shot ask.
- `EditorViewNavigation.ZoomSteps` adds up wheel notches and is applied about the
  pointer at update time; it is an accumulator, not a flag.
- `Playhead.SeekRequestMs` is the playhead's own seek. It belongs with the
  host-interface step.
- `UiColorField.Committed` / `Dropped` are "changed since the owner last
  looked" flags read by `SelfModulate`; they go when value fields report
  through signals as well.
- The editor LEVEL INFO swatches are drop targets that nobody listens to
  (before and after).

First-sight listening: systems that own the reaction but don't build the
entity (`EditorDialogSystem`, `PlaySkipSystem`) add the handler the first
time their query sees the entity (a `HashSet<int>` of ids). A signal sent
before that first update is lost: the skip only on a session's first frame.

**Next:** `ToolAction` + `AnimatorCommands.Run` (done, §13.2), then `EditorCommand` +
`EditorCommandBus` (one payload per intent; `EditorAsked`'s enum splits there too).

### 13.2 Step 4a: `ToolAction` → intents (2026-11-23)

Done, all game projects build (Host, Animator, Labs, DevTools). One animator
screenshot (`--clip --easing --shortcuts`): the shortcut sheet and clip pick
come through the new path. Button clicks were not exercised: check by hand.

- **Gone:** `ToolAction`, `ToolActionKind`, `ToolActions`, `EnumInt`
  (`Model/Animator/ToolActions.cs`), the 127-case `AnimatorCommands.Run`
  switch and `AnimatorCommands.ChangesRig`.
- **Intents** are `readonly record struct`s in `Model/Animator/Intents/`
  (Session, Key, Clip, Rig, Flow; about 110). Up/down pairs became one record
  with a signed step (`StepBrushRadius(int By)`, `TurnSelected(int Steps)`,
  `StepClipLength(int Direction, int Size)`); `LimbTarget(verb)` split into
  one intent per verb (`AddOrbit`, `FlipTarget`, …; the Pin verb is `PlantBone`).
  `Play`/`Stop` are `StartPlaying`/`StopPlaying` (`Play` clashes with the
  `Xanadu.Screens.Play` namespace).
- **`IChangesRig`** (marker interface on the record) replaces the `ChangesRig`
  list: the EDIT RIG gate and the shortcut sheet's grouping both read it.
- **UI atoms** (`UI/Ecs/Ui/Intent.cs`): `IIntent` with `EmitOn(Entity)`,
  `IIntent<TSelf>` whose default method emits the record as its own signal
  type, the `Intent { IIntent Value }` component a button carries, and
  `UiIntents.TryGetIntent<T>` / `All`. The editor can reuse them in step 4b.
- **Emitting:** `AnimatorActionSystem` emits a pressed button's `Intent` on the
  session after its loop; `AnimatorShortcutSystem` emits `binding.Intent`
  (`AnimatorBindings.Binding(IIntent Intent, Key, …)`); the CLI, timeline host,
  ease picker, bone menu and rig tree call `session.EmitSignal(new X(…))` or `EmitOn`.
- **Hearing:** each command class has `Listen(Entity session)` registering its
  own intents with `session.On<T>(…)`; `AnimatorCommands.Listen` calls them
  all from `AnimatorSessionSetup.Create`. `On<T>` first runs
  `AnimatorPendingPicks.Dismiss` (now type patterns on the intent), then the
  `IChangesRig` gate, then the handler, the same order `Run` had.
- **Lit state and tooltips** read the intent by type: `AnimatorTooltips.For(IIntent)`
  is a type-pattern switch; the flow rows, curve buttons and preview bar
  match `AllowTransition`, `SetTransitionCurve`, `ToggleSlow`, … on the button.

Kept, on purpose:
- `ToggleMode(AnimatorMode)` still carries the mode enum (and
  `AnimatorModeTags` maps it): it is a selector (§11 kind 4) and goes with the
  `Switches` tags once the mode buttons and bindings take `Tags` directly.
- `AnimatorTargetVerb` stays as the internal switch in `AnimatorTargetCommands.Run`
  and for `AnimatorLayout.TargetTooltip`/`TargetIntent`.
- `SelectBone(int)` / `SelectPart(int)` / `SelectChain(int)` / `SelectCloth(int)`
  and `MoveLayer(int Row, …)` still pass indexes (rule 3); they become entity
  links with the rig as entities (phase 4).
- `Intent` holds the record boxed behind an interface. It is data (a value
  record), not a host channel, so rule 6 doesn't apply; it replaces the
  animator's use of `UiCommand`, which stays for everything else until its own step.
- Tests (`Tests/Animation/Tool`) still use `ToolAction`; left per the prototyping rule.

### 13.3 Step 4b: `EditorCommand` → intents (2026-11-23)

Done, all game projects build (Host, Animator, Labs, DevTools, ShaderTool).
One editor screenshot: the editor and song picker come up, no exceptions.
Button presses, shortcuts, the wheel, group panel and recovery were not
exercised: check by hand.

- **Gone:** `EditorCommand`, `EditorCommandType`, `EditorCommandBus` (and the
  bus passed through `XanaduSystemRoot`, `EditorSystemGroup`, `HostGame`, the
  overlay and nine systems), the three `TryApply` switches, and `EditorAsked`.
  `ApplyLevel` and `LoadSong(path)` had no senders and went with them.
- **Intents** are `readonly record struct`s on the UI's `IIntent<T>` in
  `Model/Editor/Intents/` (Level, Group, Screen; 35). Renamed where the old
  name hid what it does: `SetTarget` → `SetNoteTarget`, `LoadSongById` → `UseSong`,
  `SetPlacementMode` → `PickPlacement`, `ToggleGroupEase(bool)` → `ShowGroupEase`.
- **`EditorAsked` split** into `SaveLevel`, `SaveQuietly`, `OpenSongPicker`,
  `PreviewLevel(FromPlayhead)`, `LeaveEditor`, `ToggleLevelInfo`. The LEVEL
  INFO colour buttons no longer emit anything; nobody ever listened.
- **Emitting:** every sender emits on the editor entity (`EmitSignal`, or
  `EmitOnEditor` where there may be no editor). Level intents may be emitted
  inside a loop: their handlers only queue (below). Screen intents still go
  out after the loop, as their listeners show panels.
- **Hearing:** `EditorCommandSystem` has the level, selection and group
  handlers listen on each editor the first time its query sees it (keyed by
  id and revision, so a rebuilt scene reusing an id is heard again).
  `EditorCommandContext.On<T>` queues what it hears; `Run` applies it on the
  system's update in the order heard, then the `AfterQuery` edits. This keeps
  the bus's timing: nothing edits the level under another system's loop.
  Intents for an editor that is gone are dropped (the bus was cleared on
  scene change).
- The overlay keeps its own queue of actions, run on `Tick` (scene swaps).
  Its listen check is now by id and revision too.

Kept, on purpose:
- `EditorButtonKind` + `UiCommand` on the editor's buttons: the button →
  intent mapping is still the `EditorButtonSystem` switch. Buttons carrying an
  `Intent` component directly belong with the `UiCommand` step.
- `PickGroup(int Index)` passes an index into `LevelGroups`; it becomes a
  link once groups are entities.
- No test named the old types; tests that build editor systems may need
  their constructor arguments trimmed (the bus is gone), left per the prototyping rule.

**Next:** host interfaces (rule 6), one at a time, `TimelineHost` first; or
editor buttons carrying `Intent` directly, which unblocks `UiCommand`'s removal.

### 13.4 Step 5a: `EditorShortcutHost` → `KeyPressed` + intents (2026-11-23)

Done, game projects build; editor run comes up clean. Keys were not
exercised (no scripted key input): check Ctrl+S/Z/Y/C/V, F, Escape, Enter,
Delete, arrows, 0–9 and the timeline keys by hand.

- **Gone:** `IEditorShortcutTarget`, `EditorShortcutHost`, the `EditorShortcut`
  enum, `ViewFitAsked` (now the `FitView` intent).
- **UI atoms** (`UI/Systems/Input/KeyPressed.cs`): `KeyPressed(KeyChord)` signal and
  `KeyTarget` tag. `AppShortcutSystem` hands a key to the entity tagged
  `KeyTarget` as a signal; the overlay tags the editor when it listens and
  untags it on `Close`.
- **Bindings are intents:** `EditorShortcuts.Binding(KeyChord, IIntent, Scope)`.
  The overlay's `KeyPressed` handler works out the live scopes and emits the
  binding's intent on the editor, so a key and a button reach the same handler.
- New intents for what only keys did: `CloseEditorDialog`, `ToggleShortcutHelp`,
  `GoForward`, `FitView`, `TimelineKey(TimelineCommand)`.
- The overlay hears `Undo`/`Redo`/`PickPlacement` for its status line, so
  the toolbar's undo/redo and placement picks now show it too.
- Preview, save and back from keys now wait for the overlay's `Tick` like
  the buttons did, instead of swapping the scene inside `AppShortcutSystem`.

Kept: `TimelineKey` carries the timeline's command enum; it goes with `TimelineHost`.

### 13.5 Step 5b: `TimelineHost` → `TimelineFrame` + signals (2026-11-23)

User's pick: a per-frame view component. Done; Host, Animator, Labs,
DevTools and ShaderTool build; one animator and one editor run come up
with the timeline drawn and no exceptions. Presses, drags, scrubbing, the
wheel and the timeline keys were not exercised: check by hand in all three
timelines (editor strip and group keys, animator rows, shader tool clips).

- **Gone:** `ITimelineHost`, `ITimelineInput`, the `TimelineHost` component.
- **Read side** (`UI/Systems/Timeline/TimelineFrame.cs`): `TimelineFrame`
  holds the clock (now, duration, step, step origin), `Blocked`, the area
  (`Shown`, `Panel`, `Ruler`), `Markers`, the frame's `Scene`, and the
  gesture in progress. `TimelineFrameSystem`, first in `TimelineSystemGroup`,
  emits `TimelineFrameDue` on every timeline; the owner writes the frame in
  its handler. After a row scroll or a scrub pan the input system asks again
  (`TimelineFrame.Refresh`). It is derived output, never written back.
- **Action side:** `TimelineSeek`, `TimelineTogglePlay`, `TimelineNoMarker`,
  `TimelineGestureStarted/Ended`, `TimelineRowsScrolled`, `TimelineScrubbed`,
  `TimelineLabelPressed`, `TimelineLanePressed`, `TimelineItemPressed/Moved/Released`,
  `TimelineBoxSelected`, emitted on the timeline entity. Owners hear only
  what they act on. `TimelineShortcuts.Run(Entity, TimelineCommand)` reads the
  frame and emits.
- **Owners:** `EditorTimeline` (was `EditorTimelineHost`; now made by
  `EditorTimelineSystem` the first time it sees an editor, no longer by the
  overlay, so the overlay lost its history argument), `AnimatorTimeline`
  (was `AnimatorTimelineHost`; `AnimatorSessionSetup.Create` has it listen),
  and the shader tool's `ClipTimeline`. Each is a listener object held only
  by its handlers, not by a component.

Kept: `TimelineScene` is still a C# object built each frame (items carry
`object` refs: entities, group keys, event flags). It is the frame's
description, not owned data. `TimelineKey(TimelineCommand)` keeps the enum
as the shared timeline's own verb list.

Tests: `UI/Tests/TimelineShortcutsTests` and `TimelineZoomAboutPointerTests`
still build fake `ITimelineHost`s; left per the prototyping rule.

**Next:** the other host interfaces: `XifEditor.Host`, `ParticleEditor.Host`,
`EasePicker.Host`, `TextEditor.Host`, `AnimatorDocument.Files`.
