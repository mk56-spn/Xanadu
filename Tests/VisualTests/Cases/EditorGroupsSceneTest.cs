using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Scenes;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.VisualTests;

/// <summary>
/// Groups in the editor: the sidebar grid opens a group's overlay, selected
/// items join it, effects are added and retimed on the snapping timeline, and
/// the overlay closes again.
/// </summary>
public sealed class EditorGroupsSceneTest : SceneTest
{
    private Entity _note;
    private Entity _start;

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
