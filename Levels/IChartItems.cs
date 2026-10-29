using System.Collections.Generic;

namespace Xanadu.Levels;

/// <summary>
/// The placed items a single chart carries: a level document edited on its
/// own, or one variant inside an <see cref="XmfMapset"/>. Lets normalization
/// run the same way over either.
/// </summary>
public interface IChartItems
{
    List<NoteDocument> Notes { get; set; }

    List<MineDocument> Mines { get; set; }

    List<StartPointDocument> StartPoints { get; set; }

    List<BlockDocument> Blocks { get; set; }

    List<SpikeDocument> Spikes { get; set; }

    List<GroupDocument> Groups { get; set; }

    /// <summary>The playfield background as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    string BackgroundColor { get; set; }

    /// <summary>The shader slot, from the owning shader pool, the player is drawn with, or 0.</summary>
    int PlayerShader { get; set; }

    /// <summary>The shader slot, from the owning shader pool, the playfield background is drawn with, or 0.</summary>
    int FieldShader { get; set; }
}
