using System.Collections.Generic;
using System.Threading.Tasks;
using Xanadu.Ecs;
using Xanadu.Host;
using Xanadu.Host.Tools;
using Xanadu.Tools.Atlas;
using Xanadu.Tools.Autopsy;
using Xanadu.Tools.Moves;

namespace Xanadu.Tools.DevTools;

/// <summary>How the dev tools open: which tab, and each tool's own start.</summary>
internal sealed record DevToolsOptions(string Root, int Tab, AtlasOptions Atlas, bool Methods);

/// <summary>A dev tool's tab: it takes the whole body, draws its own panels, and is kept as it was left.</summary>
internal sealed record DevToolTab(string Name) : IToolTab
{
    public bool FullWidth => true;

    public string[] Notes => [];

    public bool Keep => true;
}

/// <summary>
/// The dev tools: one window for looking at the project, on the shared tabbed
/// tool shell. ATLAS charts the project's types (what touches what); AUTOPSY
/// takes apart the frames each exe saved when it last closed; MOVES charts
/// the character's move system, by hand.
/// </summary>
internal sealed class DevToolsApp : TabbedToolApp, ICodeLinks
{
    public const int AtlasTab = 0, AutopsyTab = 1, MovesTab = 2;

    private readonly DevToolsOptions _options;

    /// <summary>Read in the background from the start: Autopsy's links need it, and Atlas opens on it.</summary>
    private readonly Task<CodeMap> _map;

    /// <summary>A type Atlas should go to when it is next built.</summary>
    private string? _pendingFocus;

    public DevToolsApp(HostWindowOptions window, DevToolsOptions options)
        : base("Xanadu Dev Tools", window, [new DevToolTab("ATLAS"), new DevToolTab("AUTOPSY"), new DevToolTab("MOVES")], options.Tab)
    {
        _options = options;
        _map = Task.Run(() => CodeMap.Read(options.Root));
    }

    /// <summary>Looking at captures shouldn't make one of its own.</summary>
    protected override bool RecordsAutopsy => false;

    protected override IToolStage CreateStage(int tab, Handle parent, Rect2 area)
    {
        var context = new DevToolsContext(Keyboard, TextInput);
        if (tab == AutopsyTab)
            return new AutopsyStage(context, parent, area, this, _options.Methods);
        if (tab == MovesTab)
            return new MovesStage();

        AtlasOptions start = _pendingFocus is { } focus
            ? _options.Atlas with { Focus = focus, Compare = null, Similar = false }
            : _options.Atlas;
        _pendingFocus = null;
        return new AtlasStage(context, _map.Result, start);
    }

    public bool Ready => _map.IsCompletedSuccessfully;

    public CodeLink? Resolve(string name, CodeLink? parent)
    {
        if (!Ready)
            return null;

        CodeMap map = _map.Result;
        CodeNode? owner = parent is null ? null : map.Find(parent.Type);
        foreach (string candidate in new[] { name, name + "System", name + "SystemGroup", name + "Group" })
        {
            if (map.Find(candidate) is not { Kind: NodeKind.System or NodeKind.Group } node)
                continue;
            if (owner is not null && !Uses(owner, node))
                continue;
            return new CodeLink(node.FullName, node.Kind == NodeKind.Group);
        }

        return null;
    }

    /// <summary>Whether <paramref name="owner"/> touches <paramref name="node"/> at all: a group makes the systems it holds.</summary>
    private static bool Uses(CodeNode owner, CodeNode node)
    {
        foreach (SortedSet<int> ids in owner.Out.Values)
        {
            if (ids.Contains(node.Id))
                return true;
        }

        return false;
    }

    public void Open(string type)
    {
        if (StageOf(AtlasTab) is AtlasStage atlas)
            atlas.Show(type);
        else
            _pendingFocus = type;
        Open(AtlasTab);
    }
}
