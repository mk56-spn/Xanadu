using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Ui.Systems.Canvas;
using Handle = Xanadu.Core.Maths.Handle;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// The shader tool's picker: every stack as a card (a live thumbnail over its name)
/// in a scrolling grid, a tab per folder (Game, Lab, Learn…; <see cref="UiTabs"/>), on a
/// scrim over the whole editor, hidden until the bar's SHADERS button opens it on the
/// open stack's tab. A right-click on a card offers RENAME (a field in place of its name;
/// <see cref="Renamed"/> hands back what was typed) and DELETE, which asks first; the app reads
/// the picks from <see cref="TakeMenuPick"/> and does the disk's part. A press on a card, on CLOSE or on the scrim around the
/// panel hides it again; the app reads the presses from <see cref="Cards"/>,
/// <see cref="Thumbs"/>, <see cref="NewButton"/> and <see cref="Scrim"/>.
/// </summary>
internal sealed class ShaderGrid
{
    private const int Columns = 5;
    private const float ThumbWidth = 288, ThumbHeight = 162;
    private static readonly Color ScrimColour = new(0f, 0f, 0f, 0.6f);

    private readonly UiNode _scrim;
    private readonly UiNode _strip;
    private readonly UiNode _pages;
    private readonly List<Entity> _cards = [];
    private readonly List<Entity> _thumbs = [];

    /// <summary>Each stack's whole card, by file index: what the right-click menu sits on.</summary>
    private readonly List<Entity> _whole = [];

    /// <summary>A card's menu for <paramref name="file"/>: its entries' places are <see cref="Rename"/> and <see cref="Delete"/>.</summary>
    private static MenuItems CardMenu(ShaderFile file) => new([new MenuItem("RENAME"), new MenuItem("DELETE", Confirm: true, $"DELETE {file.Name}?")]);

    public const int Rename = 0, Delete = 1;

    /// <summary>The last menu pick not yet taken: the card's file index and the entry.</summary>
    private (int File, int Item)? _menuPick;

    /// <summary>The card being renamed and its field, while the field is up.</summary>
    private (int File, Entity Field)? _renaming;

    /// <summary>Each folder's tab button and grid, in the order first met.</summary>
    private readonly List<(string Folder, Entity Button, UiNode Grid)> _tabs = [];

    /// <summary>Each card's tab, by file index.</summary>
    private readonly List<int> _tabOf = [];

    private int _picked = -1;

    public ShaderGrid(UiNode layer)
    {
        _scrim = layer.Column().Cover().Fill(ScrimColour).Input()
            .Pad(UiMetrics.Space.Xl).Cross(StackCrossAlignment.Center).Main(StackMainAlignment.Center);
        UiNode panel = _scrim.Panel().Input().Gap(UiMetrics.Space.Md).Cross(StackCrossAlignment.Stretch).Grow();
        UiNode head = panel.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        head.Text("SHADERS", UiType.Section).AccentText().Grow();
        NewButton = head.Button("NEW", UiType.Small).Secondary().Tooltip("Make a new effect from the template and open it");
        CloseButton = head.Button("CLOSE", UiType.Small).Secondary().Tooltip("Back to the open shader (Escape)");
        _strip = panel.Row().Gap(UiMetrics.Space.Xs);
        UiNode pages = default;
        panel.Add(new Scroll(grow: 1, stretch: true) { new Custom(p => pages = p.Column().Cross(StackCrossAlignment.Start)) });
        _pages = pages;
        _scrim.Hidden();
    }

    public Entity Scrim => _scrim;
    public Entity NewButton { get; }
    public Entity CloseButton { get; }

    /// <summary>Each stack's name button, by file index.</summary>
    public IReadOnlyList<Entity> Cards => _cards;

    /// <summary>Each stack's thumbnail, by file index; a press on it picks too.</summary>
    public IReadOnlyList<Entity> Thumbs => _thumbs;

    public bool Open { get; private set; }

    /// <summary>Opens the grid on the first frame (<c>--picker</c>), as if SHADERS were pressed.</summary>
    public static bool StartOpen { get; set; }

    /// <summary>Whether to open now for <see cref="StartOpen"/>; true once.</summary>
    public static bool TakeStartOpen()
    {
        bool open = StartOpen;
        StartOpen = false;
        return open;
    }

    /// <summary>Opens on the open stack's tab, or closes.</summary>
    public void Show(bool open)
    {
        Open = open;
        UiLayout.SetHidden(_scrim, !open);
        if (open && _picked >= 0 && _picked < _tabOf.Count)
            _strip.Entity.GetComponent<UiTabs>().Selected = _tabOf[_picked];
    }

    /// <summary>The folder a stack's card goes under: the first part of its name.</summary>
    private static string FolderOf(ShaderFile file) => file.Name.IndexOf('/') is var slash and > 0 ? file.Name[..slash] : "Other";

    /// <summary>The tab for <paramref name="folder"/>, made with its grid the first time.</summary>
    private int TabFor(string folder)
    {
        int tab = _tabs.FindIndex(made => made.Folder == folder);
        if (tab >= 0)
            return tab;
        Entity button = _strip.Button(folder.ToUpperInvariant(), UiType.Small).Secondary().Tooltip($"The stacks under Assets/Shaders/{folder}");
        UiNode grid = _pages.Grid(Columns, UiMetrics.Space.Md, UiMetrics.Space.Md).Hidden();
        _tabs.Add((folder, button, grid));
        // The strip's component is its buttons and bodies, so it is made again with every tab.
        int selected = _strip.Entity.TryGetComponent(out UiTabs had) ? had.Selected : 0;
        _strip.Entity.AddComponent(new UiTabs([.. _tabs.Select(made => made.Button)], [.. _tabs.Select(made => made.Grid.Entity)], Vector2.Zero, selected));
        return _tabs.Count - 1;
    }

    /// <summary>
    /// A stack's card. The effect as shipped, drawn live with its own defaults, or a still
    /// (<paramref name="still"/>) for one too heavy to draw live; a box the thumbnail's size
    /// clips it, as an effect that moves its own vertices (a deform) would draw past it.
    /// </summary>
    public void Add(ShaderFile file, Handle? still)
    {
        int tab = TabFor(FolderOf(file));
        _tabOf.Add(tab);
        UiNode card = _tabs[tab].Grid.Column().Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Stretch).Width(ThumbWidth);
        UiNode frame = card.Column().Size(ThumbWidth, ThumbHeight).Clip();
        var size = new Vector2(ThumbWidth, ThumbHeight);
        _thumbs.Add((still is { } texture
                ? frame.Image(texture, size)
                : frame.Rect(Colors.White, size: size).With(new UiMaterial(new CanvasMaterial(file.Path))))
            .Input()
            .Tooltip(file.Path));
        _cards.Add(card.Button(file.Name, UiType.Small).Quiet());
        int index = _whole.Count;
        _whole.Add(card.With(CardMenu(file)).Entity.OnMenuPicked(item => _menuPick = (index, item)));
    }

    /// <summary>The last right-click pick on a card, once: its file index and the entry (<see cref="Rename"/>, <see cref="Delete"/>).</summary>
    public (int File, int Item)? TakeMenuPick()
    {
        (int File, int Item)? pick = _menuPick;
        _menuPick = null;
        return pick;
    }

    /// <summary>Puts a field holding the stack's name (its last part) in place of card <paramref name="index"/>'s name, with the keys.</summary>
    public void BeginRename(int index, ShaderFile file)
    {
        EndRename();
        UiNode card = UiNode.Wrap(_whole[index]);
        UiLayout.SetHidden(_cards[index], true);
        Entity field = card.Add(new Field(file.Name[(file.Name.LastIndexOf('/') + 1)..], UiType.Small, width: ThumbWidth,
            height: UiMetrics.Control.Sm)).Node.Entity;
        field.AddTag<TextFieldFocused>();
        _renaming = (index, field);
    }

    /// <summary>The name typed for the card being renamed, once its field lets go of the keys; null until then.</summary>
    public (int File, string Typed)? Renamed()
    {
        if (_renaming is not { } renaming || renaming.Field.Tags.Has<TextFieldFocused>())
            return null;
        string typed = renaming.Field.GetComponent<TextField>().Value.Trim();
        EndRename();
        return (renaming.File, typed);
    }

    private void EndRename()
    {
        if (_renaming is not { } renaming)
            return;
        UiLayout.DeleteSubtree(renaming.Field);
        UiLayout.SetHidden(_cards[renaming.File], false);
        _renaming = null;
    }

    /// <summary>Card <paramref name="index"/> shows <paramref name="file"/> now: its name, and its thumbnail drawing the stack's new path.</summary>
    public void Retarget(int index, ShaderFile file, bool still)
    {
        UiText.Set(_cards[index], file.Name);
        _whole[index].AddComponent(CardMenu(file));
        UiNode thumb = UiNode.Wrap(_thumbs[index]).Tooltip(file.Path);
        if (!still)
            thumb.With(new UiMaterial(new CanvasMaterial(file.Path)));
    }

    /// <summary>Takes card <paramref name="index"/> out of the grid: its stack is gone. Its place stays, so the rest keep theirs.</summary>
    public void Remove(int index) => UiLayout.SetHidden(_whole[index], true);

    /// <summary>Bright blue only for the one open; the rest at rest.</summary>
    public void Mark(int picked)
    {
        _picked = picked;
        for (int i = 0; i < _cards.Count; i++)
        {
            UiNode card = UiNode.Wrap(_cards[i]);
            if (i == picked)
                card.Primary();
            else
                card.Quiet();
            _cards[i].AddTag<Dirty>();
        }
    }

    public void Name(int index, string name) => UiText.Set(_cards[index], name);
}
