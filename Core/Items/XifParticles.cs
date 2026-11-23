using Xanadu.Core.Particles;

namespace Xanadu.Core.Items;

/// <summary>
/// The particles a node streams from its spot while it shows: the node's own
/// settings (how many, how long they live, how they are thrown and pulled,
/// what they look like), not a name in the particle library. The library only
/// keeps presets to start from. The host's particle server simulates them.
/// </summary>
public sealed class XifParticles : XifModule
{
    public XpfParticle Settings = new();

    public override XifModule Copy() => new XifParticles { Settings = Settings.Clone() };

    public override bool Same(XifModule other) => other is XifParticles particles && particles.Settings.Same(Settings);
}
