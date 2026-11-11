namespace Xanadu.Tools.DevTools;

/// <summary>A system or group Atlas can open: its full type name, and whether it is a group.</summary>
internal sealed record CodeLink(string Type, bool Group);

/// <summary>How a tool reaches Atlas: which type a timed name is, and going there.</summary>
internal interface ICodeLinks
{
    /// <summary>False while the chart is still being read; nothing resolves until it is.</summary>
    bool Ready { get; }

    /// <summary>
    /// The system or group a track is named after, or null. Inside a known
    /// <paramref name="parent"/> it has to be one the parent uses, so a step
    /// that only shares a type's name doesn't link to it.
    /// </summary>
    CodeLink? Resolve(string name, CodeLink? parent);

    /// <summary>Switches to ATLAS focused on <paramref name="type"/>.</summary>
    void Open(string type);
}
