using System.Collections.Generic;

namespace Xanadu.Ui.Systems.Debug;

/// <summary>One part of the frame the host times: its name, and how deep it sits under the parts above it.</summary>
public readonly record struct FramePhase(string Name, int Depth);

/// <summary>
/// Where the frame goes, as the host measures it: every part of its update and
/// draw, and what is left outside its code. Times only ever add up, like a
/// system's perf, so the overlay averages over its own windows by difference.
/// </summary>
public interface IFramePhaseSource
{
    /// <summary>The parts, in the order the overlay lists them; a part's time includes the deeper ones under it.</summary>
    IReadOnlyList<FramePhase> Phases { get; }

    /// <summary>Every millisecond spent in a part so far.</summary>
    double TotalMs(int phase);

    /// <summary>Frames drawn so far.</summary>
    long Frames { get; }
}
