using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs.Components;

/// <summary>
/// Marks an entity whose <see cref="UiMaterial"/> reacts to the song preview:
/// each frame its shader gets a four-component <c>energy</c> uniform, the
/// loudness of four slices of the spectrum, lowest frequencies first.
/// </summary>
public struct SpectrumEnergy : IComponent;
