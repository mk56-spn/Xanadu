using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Groups in the editor: the sidebar grid opens a group's overlay, selected
/// items join it, effects are added and retimed on the snapping timeline, and
/// the overlay closes again.
/// </summary>
public sealed class EditorGroupsSceneTest : SceneTest
{
    private Entity _note;
    private Entity _start;
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

        AddStep("click group 3", () => Click(Cell(2)));
        AddWaitStep("hold group 3", 2);
        AddStep("release group 3", () => Context.Pointer.Release());
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

        AddStep("select the colour row", () => Click(Piece(EditorGroupPart.RowBar, 1)));
        AddWaitStep("hold the colour bar", 2);
        AddStep("release the colour bar", () => Context.Pointer.Release());
        AddUntilStep("the colour is selected", () => State().SelectedEffect == 1);

        AddStep("open the colour picker", () => Click(Piece(EditorGroupPart.SelectionPick)));
        AddWaitStep("hold PICK", 2);
        AddStep("release PICK", () => Context.Pointer.Release());
        AddUntilStep("the palette opens", () => State().PaletteOpen && Piece(EditorGroupPart.PalettePanel).GetComponent<Position>().Value != UiLayout.Hidden);

        AddStep("pick a yellow swatch", () => Click(Piece(EditorGroupPart.PaletteSwatch, 4)));
        AddWaitStep("hold the swatch", 2);
        AddStep("release the swatch", () => Context.Pointer.Release());
        AddUntilStep("the colour changes", () => Group().Effects[1].Tint.G > 0.7f);

        AddStep("close the palette", () => Click(Piece(EditorGroupPart.PaletteClose)));
        AddWaitStep("hold palette close", 2);
        AddStep("release palette close", () => Context.Pointer.Release());
        AddUntilStep("the palette closes", () => !State().PaletteOpen);

        AddStep("select the move row", () => Click(Piece(EditorGroupPart.RowBar, 0)));
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

        AddStep("select the move row again", () => Click(Piece(EditorGroupPart.RowBar, 0)));
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

        AddStep("reopen group 3", () => Click(Cell(2)));
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
                       groups.Groups, member.Mask, 2f, out Color mix, out _) &&
                   mix.G > 0.7f;
        });

        AddStep("press the move's bar", () => Click(Piece(EditorGroupPart.RowBar, 0)));
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

        AddStep("turn the target preview off", () => Click(PreviewButton()));
        AddWaitStep("hold PREVIEW off", 2);
        AddStep("release PREVIEW off", () => Context.Pointer.Release());
        AddUntilStep("the target preview turns off", () => !State().Preview);

        AddStep("click PREVIEW", () => Click(PreviewButton()));
        AddWaitStep("hold PREVIEW", 2);
        AddStep("release PREVIEW", () => Context.Pointer.Release());
        AddUntilStep("the group preview turns on", () => State().Preview);
        AddStep("seek to five seconds", () => Seek(5_000));
        AddWaitStep("look at the preview", 5);
        AddStep("click PREVIEW again", () => Click(PreviewButton()));
        AddWaitStep("hold PREVIEW again", 2);
        AddStep("release PREVIEW again", () => Context.Pointer.Release());
        AddUntilStep("the group preview turns off", () => !State().Preview);
    }

    private Entity Editor() =>
        Context.Entities.Query<EditorGroupState>().ToEntityList()[0];

    private EditorGroupState State() => Editor().GetComponent<EditorGroupState>();

    private Entity PreviewButton() => Editor().GetComponent<EditorGroupGrid>().Preview;

    private Vector2 RulerPoint(float seconds)
    {
        Vector2 screen = Editor().GetComponent<PlayfieldView>().ScreenSize;
        Vector2 dialog = EditorGroupLayout.DialogSize(screen);
        Vector2 origin = (screen - dialog) / 2;
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

    private Entity Cell(int index)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupCell>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupCell>().Index == index)
                return entity;
        }

        return default;
    }

    private Entity Piece(EditorGroupPart part, int row = 0)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupPiece>().ToEntityList())
        {
            EditorGroupPiece piece = entity.GetComponent<EditorGroupPiece>();
            if (piece.Part == part && piece.Row == row)
                return entity;
        }

        return default;
    }

    private bool Visible(EditorGroupPart part, int row = 0) =>
        Piece(part, row).GetComponent<Position>().Value != UiLayout.Hidden;

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
