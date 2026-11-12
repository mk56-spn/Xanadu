using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The editor's layers (Docs/render-architecture.md step 4): items on several
/// layers, the selection's LAYER and Z steppers in the sidebar, and the layers
/// panel from the view bar with one layer hidden and one named, and the
/// group tags turned on beside the items in groups.
/// </summary>
public sealed class EditorLayersSceneTest : SceneTest
{
    private Entity _back;
    private Entity _front;
    private Entity _text;
    private Entity _note;

    public override string Name => "Editor layers";
    public override string Slug => "editor-layers";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the layers button",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorLayersPanel>().Count == 1);
        AddEditorSong();

        AddStep("place items on several layers", () =>
        {
            // Around the middle of what the view shows, which opens on the bottom of the field.
            PlayfieldView view = Editor().GetComponent<PlayfieldView>();
            Vector2 centre = view.ScreenToPlayfield(new Vector2(900, 560));
            _back = Item(new Block(centre + new Vector2(-300, -200), new Vector2(240, 240)), "#3d7bff", -2, 0);
            _front = Item(new Block(centre + new Vector2(-180, -80), new Vector2(240, 240)), "#ffd23d", -2, 1);
            _note = Item(new Note(0, centre + new Vector2(200, -100)), "#ffffff", -1, 0);
            _text = Item(new LevelText(centre + new Vector2(0, 250), "IN FRONT"), "#ff4060", 2, 0);
            Editor().GetComponent<NoteSelection>().SetMany(new[] { _back });
        });
        AddUntilStep("the sidebar shows the selection's layer", () => VisualAssert.HasText(Context.Entities, "LAYER -2"));
        AddAssert("the blend is a choice, not arrows", () =>
            !BlendButton().IsNull && VisualAssert.HasText(Context.Entities, "BLEND  MIX  ▾"));

        AddStep("select both blocks", () =>
            Editor().GetComponent<NoteSelection>().SetMany(new[] { _back, _front }));
        AddStep("open the blend choice", () => Click(BlendButton()));
        AddWaitStep("hold the blend choice", 2);
        AddStep("release the blend choice", () => Context.Pointer.Release());
        AddUntilStep("the blend list is open", () => BlendButton().GetComponent<Dropdown>().Open);
        AddStep("pick ADD", () => Click(BlendOption(BlendMode.Add)));
        AddWaitStep("hold ADD", 2);
        AddStep("release ADD", () => Context.Pointer.Release());
        AddUntilStep("the selected blocks blend additively", () =>
            ItemBlend.Of(_back) == BlendMode.Add && ItemBlend.Of(_front) == BlendMode.Add);
        AddAssert("the button shows the chosen mode", () =>
            VisualAssert.HasText(Context.Entities, "BLEND  ADD  ▾"));
        AddAssert("the choice is one undo step", () => Context.Systems.EditorHistory.CanUndo);

        AddStep("open the layers panel", () => Click(Panel().Toggle));
        AddWaitStep("hold LAYERS", 2);
        AddStep("release LAYERS", () => Context.Pointer.Release());
        AddUntilStep("the panel is open", () => Layers().Open && Panel().Panel.Enabled);

        AddStep("hide layer 2", () => Click(RowFor(2).Hide));
        AddWaitStep("hold H", 2);
        AddStep("release H", () => Context.Pointer.Release());
        AddUntilStep("layer 2 is hidden", () => !Layers().Shows(2));

        AddStep("name layer -2", () =>
        {
            Editor().GetComponent<EditorLayers>().Current = -2;
            Editor().GetComponent<LevelLayers>().SetName(-2, "TERRAIN");
        });
        AddUntilStep("the row shows its name", () => System.Linq.Enumerable.Any(Context.Entities.Query<Xanadu.Ui.Ecs.Ui.Text>().ToEntityList(), text => text.GetComponent<Xanadu.Ui.Ecs.Ui.Text>().Value.Contains("TERRAIN")));
        AddAssert("the yellow block is still over the blue one", () =>
            _front.GetComponent<ItemLayer>().Z > _back.GetComponent<ItemLayer>().Z);
        AddAssert("the text keeps its layer", () => _text.GetComponent<ItemLayer>().Layer == 2);
        AddStep("put the blocks and note in groups", () =>
        {
            _back.AddComponent(new ItemGroups(ItemGroups.Bit(0) | ItemGroups.Bit(1)));
            _front.AddComponent(new ItemGroups(ItemGroups.Bit(15)));
            _note.AddComponent(new ItemGroups(ItemGroups.Bit(4)));
        });
        AddAssert("tags start off", () => !GroupTagsSwitch.IsOn(Context.Entities));
        AddStep("turn the group tags on", () => Click(TagsButton()));
        AddWaitStep("hold TAGS", 2);
        AddStep("release TAGS", () => Context.Pointer.Release());
        AddUntilStep("the tags are on", () => GroupTagsSwitch.IsOn(Context.Entities));
        AddWaitStep("look at the panel and tags", 5);
    }

    private Entity Item<T>(T component, string color, int layer, int z) where T : struct, IComponent
    {
        Entity item = EditorItems.Create(Context.Entities, component);
        item.GetComponent<SelfModulate>() = new SelfModulate(new Color(color));
        ItemLayer.Put(item, new ItemLayer(layer, z));
        return item;
    }

    private Entity Editor() => Context.Entities.Query<EditorLayersPanel>().ToEntityList()[0];

    private EditorLayersPanel Panel() => Editor().GetComponent<EditorLayersPanel>();

    private EditorLayers Layers() => Editor().GetComponent<EditorLayers>();

    private EditorLayerRowUi RowFor(int layer)
    {
        EditorLayers layers = Layers();
        int at = EditorLayerEdits.Rows(Context.Entities, layers, Editor().GetComponent<LevelLayers>()).FindIndex(row => row.Layer == layer);
        return Panel().Rows[at - layers.Scroll];
    }

    private Entity BlendButton()
    {
        foreach (Entity entity in Context.Entities.Query<EditorDropdownControl>().ToEntityList())
        {
            if (entity.GetComponent<EditorDropdownControl>().Kind == EditorDropdownKind.Blend)
                return entity;
        }

        return default;
    }

    private Entity BlendOption(BlendMode mode)
    {
        Entity owner = BlendButton();
        foreach (Entity entity in Context.Entities.Query<DropdownOption>().WithDisabled().ToEntityList())
        {
            DropdownOption option = entity.GetComponent<DropdownOption>();
            if (option.Owner == owner && option.Value == (int)mode)
                return entity;
        }

        return default;
    }

    private Entity TagsButton() =>
        UiToggles.FindSwitch(Context.Entities, GroupTagsSwitch.Id);

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));
}
