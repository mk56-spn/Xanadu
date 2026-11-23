using System.Collections.Generic;
using System.Linq;
using Xanadu.Logic.Screens;
using Xanadu.Model.Docs;

namespace Xanadu.Logic.Docs;

/// <summary>The docs overlay's entrances. Open takes a page's index on the shelf.</summary>
public enum DocsEntrance
{
    Close,
    Open,
}

/// <summary>
/// The built-in docs with no picture of them: the pages, which one is open,
/// and the entrances a layout binds. Closing is the overlay's business, so it
/// hands that back through <paramref name="close"/>.
/// </summary>
public sealed class DocsLogic(IReadOnlyList<DocPage> pages, Action close) : IScreenLogic<DocsEntrance>
{
    /// <summary>The key that drops the docs down and puts them away, from any screen.</summary>
    public const Key Toggle = Key.Quoteleft;

    public ScreenContract<DocsEntrance> Contract { get; } = new ScreenContract<DocsEntrance>()
        .Mandatory(DocsEntrance.Close)
        .Mandatory(DocsEntrance.Open, Enumerable.Range(0, pages.Count).ToArray(), i => pages[i].Name);

    public IReadOnlyList<DocPage> Pages => pages;

    /// <summary>The index of the page showing.</summary>
    public int Current { get; private set; }

    public DocPage Page => pages[Current];

    public void Do(DocsEntrance entrance, int arg)
    {
        switch (entrance)
        {
            case DocsEntrance.Close:
                close();
                break;
            case DocsEntrance.Open when arg >= 0 && arg < pages.Count:
                Current = arg;
                break;
        }
    }
}
