using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xanadu.Model.Levels;

/// <summary>The item primitives every XMF chunk shares: counts, items and their string indexes.</summary>
internal static class XmfItemIO
{
    internal const uint MaxItems = 1_000_000;
    internal const uint MaxEffects = 100_000;
    internal const uint MaxParams = 4_096;
    internal const uint MaxChannels = 16;
}
