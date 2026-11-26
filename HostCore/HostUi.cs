using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Host;

/// <summary>How the host boots the ECS UI (<see cref="UiRoot"/>) on <see cref="HostApp.Entities"/>.</summary>
internal enum HostUi
{
    /// <summary>The UI root, with the F1, F3 and F4 overlays, stepping the exe's root (<see cref="HostApp.UseSystems"/>).</summary>
    Standard,

    /// <summary>The same, without hover tooltips (the animator draws its own).</summary>
    NoTooltips,

    /// <summary>No UI on the host's store: only the visual-test harness, which makes a store and a UI root per scene.</summary>
    None,
}
