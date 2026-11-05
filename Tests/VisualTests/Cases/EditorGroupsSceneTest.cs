using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Groups in the editor: the sidebar grid opens a group's overlay, selected
/// items join it, effects are added and retimed on the snapping timeline, the
/// overlay closes again, and the main view shows the group live at the playhead.
/// </summary>
public sealed class EditorGroupsSceneTest : SceneTest
{
    private Entity _note;
    private Entity _start;
    private Entity _block;
    private Vector2 _offsetBefore;

    public override string Name => "Editor groups";
    public override string Slug => "editor-groups";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the groups grid",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorGroupCell>().Count == GroupLimits.Count &&
                  VisualAssert.HasText(Context.Entities, "GROUPS"));
        AddAssert("the overlay starts closed", () => State().Open == -1);

        AddStep("place and select a note and a start point", () =>
        {
            _note = EditorItems.Create(Context.Entities, new Note(2_000, new Vector2(400, 400)));
            _start = EditorItems.Create(Context.Entities, new StartPoint(
                "Intro",
                2_000,
                new Vector2(300, 300),
                System.Numerics.Vector2.Zero,
                new Vector2(0, -1)));
            Editor().GetComponent<NoteSelection>().SetMany(new[] { _note, _start });
        });

        AddStep("click group 5 in the grid", () => Click(Cell(4)));
        AddWaitStep("hold group 5", 2);
        AddStep("release group 5", () => Context.Pointer.Release());
        AddUntilStep("the grid put the selection in group 5", () =>
            _note.TryGetComponent(out ItemGroups g) && g.Has(4) &&
            _start.TryGetComponent(out ItemGroups s) && s.Has(4));
        AddAssert("the grid opened nothing", () => State().Open == -1);
        AddStep("click group 5 again", () => Click(Cell(4)));
        AddWaitStep("hold group 5 again", 2);
        AddStep("release group 5 again", () => Context.Pointer.Release());
        AddUntilStep("the grid took the selection back out", () =>
            !(_note.TryGetComponent(out ItemGroups g) && g.Has(4)) &&
            !(_start.TryGetComponent(out ItemGroups s) && s.Has(4)));

        for (int i = 0; i < 2; i++)
        {
            AddStep($"step the bar up {i + 1}", () => Click(Bar().Next));
            AddWaitStep($"hold + {i + 1}", 2);
            AddStep($"release + {i + 1}", () => Context.Pointer.Release());
        }

        AddUntilStep("the bar points at group 3", () => State().BarGroup == 2);
        AddStep("click EDIT", () => Click(Bar().Edit));
        AddWaitStep("hold EDIT", 2);
        AddStep("release EDIT", () => Context.Pointer.Release());
        AddUntilStep("the overlay opens on group 3", () => State().Open == 2 && Visible(EditorGroupPart.Dialog));

        AddStep("click ADD SELECTED", () => Click(Piece(EditorGroupPart.AddSelected)));
        AddWaitStep("hold ADD SELECTED", 2);
        AddStep("release ADD SELECTED", () => Context.Pointer.Release());
        AddUntilStep("the note joined the group", () => _note.TryGetComponent(out ItemGroups g) && g.Has(2));
        AddUntilStep("the start point joined the group", () => _start.TryGetComponent(out ItemGroups g) && g.Has(2));

        AddStep("click + MOVE", () => Click(Piece(EditorGroupPart.AddMove)));
        AddWaitStep("hold + MOVE", 2);
        AddStep("release + MOVE", () => Context.Pointer.Release());
        AddUntilStep("a move was added", () => Group().Effects.Count == 1);

        AddStep("click + COLOR", () => Click(Piece(EditorGroupPart.AddColor)));
        AddWaitStep("hold + COLOR", 2);
        AddStep("release + COLOR", () => Context.Pointer.Release());
        AddUntilStep("a colour was added", () => Group().Effects.Count == 2);
        AddUntilStep("the rows are shown", () => Visible(EditorGroupPart.RowKind, 0) && Visible(EditorGroupPart.RowKind, 1));

        AddStep("click the move's EASE", () => Click(Piece(EditorGroupPart.RowEase, 0)));
        AddWaitStep("hold EASE", 2);
        AddStep("release EASE", () => Context.Pointer.Release());
        AddUntilStep("the move eases in", () => Group().Effects[0].Ease == GroupEase.In);
        AddAssert("the colour keeps linear easing", () => Group().Effects[1].Ease == GroupEase.Linear);

        AddStep("select the colour row", () => Context.Pointer.Click(Bar(1)));
        AddWaitStep("hold the colour bar", 2);
        AddStep("release the colour bar", () => Context.Pointer.Release());
        AddUntilStep("the colour is selected", () => State().SelectedEffect == 1);

        AddStep("open the colour picker", () => Click(Piece(EditorGroupPart.SelectionPick)));
        AddWaitStep("hold PICK", 2);
        AddStep("release PICK", () => Context.Pointer.Release());
        AddUntilStep("the palette opens", () => State().PaletteOpen && !UiLayout.IsHidden(Piece(EditorGroupPart.PalettePanel)));

        AddStep("pick a yellow swatch", () => Click(Piece(EditorGroupPart.PaletteSwatch, 4)));
        AddWaitStep("hold the swatch", 2);
        AddStep("release the swatch", () => Context.Pointer.Release());
        AddUntilStep("the colour changes", () => Group().Effects[1].Tint.G > 0.7f);

        AddStep("close the palette", () => Click(Piece(EditorGroupPart.PaletteClose)));
        AddWaitStep("hold palette close", 2);
        AddStep("release palette close", () => Context.Pointer.Release());
        AddUntilStep("the palette closes", () => !State().PaletteOpen);

        AddStep("select the move row", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the move bar", 2);
        AddStep("release the move bar", () => Context.Pointer.Release());
        AddUntilStep("the move is selected", () => State().SelectedEffect == 0);

        AddStep("nudge the move later", () => Click(Piece(EditorGroupPart.SelectionNudgeRight)));
        AddWaitStep("hold nudge right", 2);
        AddStep("release nudge right", () => Context.Pointer.Release());
        AddUntilStep("the move starts later", () => Group().Effects[0].Start > 0f);

        AddStep("nudge the move back", () => Click(Piece(EditorGroupPart.SelectionNudgeLeft)));
        AddWaitStep("hold nudge left", 2);
        AddStep("release nudge left", () => Context.Pointer.Release());
        AddUntilStep("the move is back at zero", () => Group().Effects[0].Start <= 0.001f);

        AddStep("mute the move", () => Click(Piece(EditorGroupPart.SelectionMute)));
        AddWaitStep("hold MUTE", 2);
        AddStep("release MUTE", () => Context.Pointer.Release());
        AddUntilStep("the move is muted", () => Group().Effects[0].Muted);

        AddStep("unmute the move", () => Click(Piece(EditorGroupPart.SelectionMute)));
        AddWaitStep("hold UNMUTE", 2);
        AddStep("release UNMUTE", () => Context.Pointer.Release());
        AddUntilStep("the move is unmuted", () => !Group().Effects[0].Muted);

        AddStep("duplicate the move", () => Click(Piece(EditorGroupPart.SelectionDuplicate)));
        AddWaitStep("hold DUP", 2);
        AddStep("release DUP", () => Context.Pointer.Release());
        AddUntilStep("the move is duplicated", () => Group().Effects.Count == 3 && State().SelectedEffect == 1);

        AddStep("delete the duplicate", () => Click(Piece(EditorGroupPart.RowDelete, 1)));
        AddWaitStep("hold duplicate delete", 2);
        AddStep("release duplicate delete", () => Context.Pointer.Release());
        AddUntilStep("the duplicate is deleted", () => Group().Effects.Count == 2);

        AddStep("select the move row again", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the move bar again", 2);
        AddStep("release the move bar again", () => Context.Pointer.Release());
        AddUntilStep("the move is selected again", () => State().SelectedEffect == 0);

        AddStep("remember the move offset", () => _offsetBefore = Group().Effects[0].Offset);
        AddStep("open the target on the canvas", () => Click(Piece(EditorGroupPart.SelectionTarget)));
        AddWaitStep("hold TARGET", 2);
        AddStep("release TARGET", () => Context.Pointer.Release());
        AddUntilStep("target editing starts", () => State().TargetEdit && State().Open == -1);

        AddStep("press the target handle", () => Context.Pointer.Click(TargetHandle()));
        AddWaitStep("hold the target", 2);
        AddStep("drag the target", () => Context.Pointer.Position += new Vector2(40, -30));
        AddWaitStep("drag the target", 3);
        AddStep("release the target", () => Context.Pointer.Release());
        AddUntilStep("the move offset changes", () => Group().Effects[0].Offset != _offsetBefore);

        AddStep("reopen group 3", () => Click(Bar().Edit));
        AddWaitStep("hold group 3 again", 2);
        AddStep("release group 3 again", () => Context.Pointer.Release());
        AddUntilStep("the overlay reopens", () => State().Open == 2 && !State().TargetEdit);

        AddStep("solo the group", () => Click(Piece(EditorGroupPart.SoloToggle)));
        AddWaitStep("hold SOLO", 2);
        AddStep("release SOLO", () => Context.Pointer.Release());
        AddUntilStep("the group is soloed", () => State().SoloGroup == 2);

        AddStep("unsolo the group", () => Click(Piece(EditorGroupPart.SoloToggle)));
        AddWaitStep("hold SOLO again", 2);
        AddStep("release SOLO again", () => Context.Pointer.Release());
        AddUntilStep("the solo is off", () => State().SoloGroup == -1);

        AddStep("play the live preview", () => Click(Piece(EditorGroupPart.PlayToggle)));
        AddWaitStep("hold PLAY", 2);
        AddStep("release PLAY", () => Context.Pointer.Release());
        AddUntilStep("the preview plays", () => Editor().GetComponent<Playhead>().Playing);

        AddStep("pause the live preview", () => Click(Piece(EditorGroupPart.PlayToggle)));
        AddWaitStep("hold PAUSE", 2);
        AddStep("release PAUSE", () => Context.Pointer.Release());
        AddUntilStep("the preview pauses", () => !Editor().GetComponent<Playhead>().Playing);

        AddStep("scrub the group timeline to 2 s", () => Context.Pointer.Click(RulerPoint(2f)));
        AddWaitStep("hold the ruler", 2);
        AddStep("release the ruler", () => Context.Pointer.Release());
        AddUntilStep("the overlay playhead moves", () =>
            System.Math.Abs(Editor().GetComponent<Playhead>().TimeMs - 2_000) < 30);
        AddAssert("the start point is tinted at two seconds", () =>
        {
            LevelGroups groups = Editor().GetComponent<LevelGroups>();
            return _start.TryGetComponent(out ItemGroups member) &&
                   GroupModulation.TryColor(
                       groups.Groups, member.Mask, 2f, out Color mix, out _, out _) &&
                   mix.G > 0.7f;
        });

        AddStep("press the move's bar", () => Context.Pointer.Click(Bar(0)));
        AddWaitStep("hold the bar", 2);
        AddStep("drag the bar right", () => Context.Pointer.Position += new Vector2(160, 0));
        AddWaitStep("drag", 3);
        AddStep("release the bar", () => Context.Pointer.Release());
        AddAssert("the bar moved later and kept its length", () =>
            Group().Effects[0].Start > 0.4f &&
            System.Math.Abs((Group().Effects[0].End - Group().Effects[0].Start) - 0.5f) < 0.01f);
        AddAssert("dragging kept the easing", () => Group().Effects[0].Ease == GroupEase.In);

        AddStep("click DONE", () => Click(Piece(EditorGroupPart.Close)));
        AddWaitStep("hold DONE", 2);
        AddStep("release DONE", () => Context.Pointer.Release());
        AddUntilStep("the overlay closes", () => State().Open == -1 && !Visible(EditorGroupPart.Dialog));

        AddStep("put a block in the group", () =>
        {
            _block = EditorItems.Create(Context.Entities, new Block(new Vector2(2300, 4400), new Vector2(240, 120)));
            _block.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        });
        AddStep("seek to five seconds", () => Seek(5_000));
        AddUntilStep("the note is shown moved by its group", () =>
            _note.TryGetComponent(out GroupOffset moved) &&
            moved.Offset.IsEqualApprox(Group().Effects[0].Offset) &&
            moved.Offset != Vector2.Zero);
        AddAssert("its placed position is untouched", () =>
            _note.GetComponent<Note>().Position == new Vector2(400, 400));
        AddUntilStep("the block is shown moved too", () => GroupOffset.Of(_block) == GroupOffset.Of(_note));
        AddStep("select only the block", () => Editor().GetComponent<NoteSelection>().SetMany(new[] { _block }));
        AddUntilStep("the start point is shown recoloured", () =>
            _start.TryGetComponent(out GroupColor tint) && tint.Coverage > 0.9f);
        AddWaitStep("look at the live editor", 5);

        AddStep("reopen group 3 over the live field", () => Click(Bar().Edit));
        AddWaitStep("hold group 3 once more", 2);
        AddStep("release group 3 once more", () => Context.Pointer.Release());
        AddUntilStep("the panel is docked at the top of the view", () =>
            State().Open == 2 &&
            Visible(EditorGroupPart.Dialog) &&
            Piece(EditorGroupPart.Dialog).GetComponent<Position>().Value.Y <
                Editor().GetComponent<PlayfieldView>().ScreenSize.Y / 3);
        AddWaitStep("look at the panel over the field", 2);

        AddStep("give the level a shader with an instance uniform", () =>
        {
            LevelShaders shaders = Editor().GetComponent<LevelShaders>();
            shaders.Slots[0] = new ShaderSlot
            {
                Name = "Track Test",
                Source = "shader_type canvas_item;\ninstance uniform vec4 tint;\nvoid fragment() { COLOR = tint; }",
            };
        });
        AddStep("step the group's shader slot on", () => Click(Piece(EditorGroupPart.ShaderSlot)));
        AddWaitStep("hold SHD", 2);
        AddStep("release SHD", () => Context.Pointer.Release());
        AddUntilStep("the group's track uses slot 1", () => Group().Shader == 1);
        AddStep("raise the tint at the playhead", () => Click(Piece(EditorGroupPart.ShaderUp)));
        AddWaitStep("hold the shader plus", 2);
        AddStep("release the shader plus", () => Context.Pointer.Release());
        AddUntilStep("the track has a tint key", () =>
            Group().ShaderKeys.Count == 1 &&
            Group().ShaderKeys[0].Param == "tint" &&
            Group().ShaderKeys[0].Size == 4 &&
            Group().ShaderKeys[0].Value.X > 0f);
        AddWaitStep("look at the shader track", 3);

        // ---------------------------------------------------------------- buffer row and lock

        AddStep("try to buffer the group across two layers", () => Click(Piece(EditorGroupPart.BufferToggle)));
        AddWaitStep("hold BUF", 2);
        AddStep("release BUF", () => Context.Pointer.Release());
        AddUntilStep("buffering is refused with the one-layer rule", () =>
            !Group().Buffered && WarningText() == GroupBufferRules.OneLayer);
        AddStep("dismiss the buffer warning", () => Click(Warning().Button));
        AddWaitStep("hold the warning OK", 2);
        AddStep("release the warning OK", () => Context.Pointer.Release());
        AddUntilStep("the buffer warning closes", () => !WarningShown());

        AddStep("put the block on the group's layer", () =>
            ItemLayer.Put(_block, new ItemLayer(ItemLayer.Of(_note).Layer, ItemLayer.Of(_block).Z)));
        AddStep("buffer the group", () => Click(Piece(EditorGroupPart.BufferToggle)));
        AddWaitStep("hold BUF again", 2);
        AddStep("release BUF again", () => Context.Pointer.Release());
        AddUntilStep("the group is buffered", () => Group().Buffered);

        AddStep("select only the note", () => Editor().GetComponent<NoteSelection>().SetMany(new[] { _note }));

        AddStep("cycle the buffer tint", () => Click(Piece(EditorGroupPart.BufferTint)));
        AddWaitStep("hold TINT", 2);
        AddStep("release TINT", () => Context.Pointer.Release());
        AddUntilStep("the buffer tint changes", () => Group().BufferTint != Colors.White);
        AddStep("drop the buffer alpha", () => Click(Piece(EditorGroupPart.BufferAlphaDown)));
        AddWaitStep("hold A-", 2);
        AddStep("release A-", () => Context.Pointer.Release());
        AddUntilStep("the buffer alpha drops", () => Group().BufferAlpha < 0.95f);
        AddStep("step the buffer blend", () => Click(Piece(EditorGroupPart.BufferBlend)));
        AddWaitStep("hold BLEND", 2);
        AddStep("release BLEND", () => Context.Pointer.Release());
        AddUntilStep("the buffer blend changes", () => Group().BufferBlend != BlendMode.Mix);

        // The group overlay's scrim covers the whole view, so the sidebar grid
        // and the layer inspector are only reachable while it is closed.
        AddStep("close the panel to reach the sidebar", () => Click(Piece(EditorGroupPart.Close)));
        AddWaitStep("hold DONE", 2);
        AddStep("release DONE", () => Context.Pointer.Release());
        AddUntilStep("the overlay closes again", () => State().Open == -1);

        AddStep("try to give the note a second group", () => Click(Cell(5)));
        AddWaitStep("hold group 6", 2);
        AddStep("release group 6", () => Context.Pointer.Release());
        AddUntilStep("the second group is refused", () =>
            WarningText() == GroupBufferRules.OneGroup &&
            !(_note.TryGetComponent(out ItemGroups mask) && mask.Has(5)));
        AddStep("dismiss the membership warning", () => Click(Warning().Button));
        AddWaitStep("hold the membership OK", 2);
        AddStep("release the membership OK", () => Context.Pointer.Release());
        AddUntilStep("the membership warning closes", () => !WarningShown());

        AddStep("try to move the note to another layer", () => Click(LayerStep(1)));
        AddWaitStep("hold LAYER +", 2);
        AddStep("release LAYER +", () => Context.Pointer.Release());
        AddUntilStep("the layer move is refused", () =>
            WarningText() == GroupBufferRules.OneLayer &&
            ItemLayer.Of(_note).Layer == -1);
        AddStep("dismiss the layer warning", () => Click(Warning().Button));
        AddWaitStep("hold the layer OK", 2);
        AddStep("release the layer OK", () => Context.Pointer.Release());
        AddUntilStep("the layer warning closes", () => !WarningShown());

        AddStep("reopen group 3", () => Click(Bar().Edit));
        AddWaitStep("hold group 3 once more", 2);
        AddStep("release group 3 once more", () => Context.Pointer.Release());
        AddUntilStep("the overlay reopens on group 3", () => State().Open == 2);

        AddStep("switch the shader row to the buffer track", () => Click(Piece(EditorGroupPart.BufferTrack)));
        AddWaitStep("hold TRACK BUF", 2);
        AddStep("release TRACK BUF", () => Context.Pointer.Release());
        AddUntilStep("the row tracks the buffer", () => State().BufferTrack);

        AddStep("give the buffer the test shader", () => Click(Piece(EditorGroupPart.ShaderSlot)));
        AddWaitStep("hold the buffer SHD", 2);
        AddStep("release the buffer SHD", () => Context.Pointer.Release());
        AddUntilStep("the buffer uses slot 1", () => Group().BufferShader == 1);
        AddStep("key a buffer value", () => Click(Piece(EditorGroupPart.ShaderUp)));
        AddWaitStep("hold the buffer plus", 2);
        AddStep("release the buffer plus", () => Context.Pointer.Release());
        AddUntilStep("the buffer track has its own key", () =>
            Group().BufferKeys.Count == 1 && Group().BufferKeys[0].Param == "tint");

        AddStep("put the buffer shader back off for the shot", () =>
        {
            // The test shader writes a solid colour, so the buffer would hide the
            // members. Walk the data back; the track was checked above.
            Group().BufferShader = 0;
            Group().BufferKeys.Clear();
        });
        AddAssert("the buffered group keeps its settings", () =>
            Group().Buffered && Group().BufferShader == 0 && Group().BufferAlpha < 0.95f);
        AddWaitStep("look at the buffer row", 4);

        AddStep("open the grid menu", () => Click(ViewBar().GridButton));
        AddWaitStep("hold the grid button", 2);
        AddStep("release the grid button", () => Context.Pointer.Release());
        AddUntilStep("the grid menu shows", () => ViewBar().Open == EditorViewMenu.Grid && ViewBar().GridMenu.Enabled);
        AddStep("turn grid snap on", () => Click(GridSnapToggle()));
        AddWaitStep("hold grid snap", 2);
        AddStep("release grid snap", () => Context.Pointer.Release());
        AddUntilStep("grid snap is on", () => ViewportSnap.IsOn(Context.Entities));

        AddStep("open the music menu", () => Click(ViewBar().MusicButton));
        AddWaitStep("hold the music button", 2);
        AddStep("release the music button", () => Context.Pointer.Release());
        AddUntilStep("the music menu replaces the grid menu", () =>
            ViewBar().Open == EditorViewMenu.Music && ViewBar().MusicMenu.Enabled && !ViewBar().GridMenu.Enabled);
        AddStep("pick 1/8", () => Click(ViewBar().DivisionButtons[2]));
        AddWaitStep("hold 1/8", 2);
        AddStep("release 1/8", () => Context.Pointer.Release());
        AddUntilStep("the beat grid is in eighths", () => Editor().GetComponent<Playhead>().SnapDivision == 16);
        AddStep("turn music snap off", () => Click(MusicSnapToggle()));
        AddWaitStep("hold music snap", 2);
        AddStep("release music snap", () => Context.Pointer.Release());
        AddUntilStep("music snap is off on the playhead", () => Editor().GetComponent<Playhead>().SnapOff);
        AddWaitStep("look at the bar and its menu", 5);
    }

    private Entity Editor() =>
        Context.Entities.Query<EditorGroupState>().ToEntityList()[0];

    private EditorGroupState State() => Editor().GetComponent<EditorGroupState>();

    /// <summary>The middle of an effect's band on the timeline, where a press grabs it whole.</summary>
    private Vector2 Bar(int row)
    {
        Vector2 screen = Editor().GetComponent<PlayfieldView>().ScreenSize;
        Vector2 origin = EditorGroupLayout.Origin(screen);
        Rect2 timeline = EditorGroupLayout.Timeline(EditorGroupLayout.DialogSize(screen));
        timeline.Position += origin;
        EditorGroupState state = State();
        return EditorGroupLayout.BarRect(Group().Effects[row], row, state.ViewStart, state.ViewSpan, timeline).GetCenter();
    }

    private Vector2 RulerPoint(float seconds)
    {
        Vector2 screen = Editor().GetComponent<PlayfieldView>().ScreenSize;
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Vector2 origin = EditorGroupLayout.Origin(screen);
        Rect2 timeline = EditorGroupLayout.Timeline(dialog);
        Rect2 ruler = EditorGroupLayout.Ruler(dialog);
        return origin + new Vector2(
            EditorGroupLayout.XForTime(timeline, 0f, 6f, seconds),
            ruler.Position.Y + ruler.Size.Y / 2);
    }

    private Vector2 TargetHandle()
    {
        EditorGroupState state = State();
        GroupDefinition group = Editor().GetComponent<LevelGroups>().Groups[state.SelectedGroup];
        GroupEffect effect = group.Effects[state.SelectedEffect];
        Entity anchor = EditorGroupTarget.AnchorMember(
            Context.Entities,
            Editor().GetComponent<NoteSelection>(),
            state.SelectedGroup);
        Vector2 basePosition = EditorGroupTarget.TryPosition(anchor, out Vector2 position)
            ? position
            : new Vector2(1000, 1000) / 2f;
        return Editor().GetComponent<PlayfieldView>().PlayfieldToScreen(
            basePosition + effect.Offset);
    }

    private GroupDefinition Group() => Editor().GetComponent<LevelGroups>().Groups[2];

    private EditorGroupBar Bar() => Editor().GetComponent<EditorGroupBar>();

    private EditorViewBar ViewBar() => Editor().GetComponent<EditorViewBar>();

    private Entity GridSnapToggle() =>
        Context.Entities.Query<Toggle>().AllTags(Tags.Get<ViewportSnapToggle>()).WithDisabled().ToEntityList()[0];

    private Entity MusicSnapToggle() =>
        Context.Entities.Query<Toggle>().AllTags(Tags.Get<EditorMusicSnapToggle>()).WithDisabled().ToEntityList()[0];

    private Entity Cell(int index)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupCell>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupCell>().Index == index)
                return entity;
        }

        return default;
    }

    private Entity LayerStep(int arg)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.LayerStep))
        {
            if (button.GetComponent<EditorButton>().Arg == arg)
                return button;
        }

        return default;
    }

    private Entity WarningEntity() =>
        Context.Entities.Query<WarningPopup>().WithDisabled().ToEntityList()[0];

    private WarningPopup Warning() => WarningEntity().GetComponent<WarningPopup>();

    private string WarningText() =>
        Warning().Message.GetComponent<Xanadu.Ui.Ecs.Ui.Text>().Value;

    private bool WarningShown() => !UiLayout.IsHidden(WarningEntity());

    private Entity Piece(EditorGroupPart part, int row = 0)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupPiece>().WithDisabled().ToEntityList())
        {
            EditorGroupPiece piece = entity.GetComponent<EditorGroupPiece>();
            if (piece.Part == part && piece.Row == row)
                return entity;
        }

        return default;
    }

    private bool Visible(EditorGroupPart part, int row = 0) =>
        !UiLayout.IsHidden(Piece(part, row));

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    private void Seek(long ms)
    {
        foreach (Entity entity in Context.Entities.Query<Playhead, EditorGroupState>().ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.TimeMs = ms;
            playhead.SeekRequestMs = ms;
            playhead.Playing = false;
        }
    }
}
