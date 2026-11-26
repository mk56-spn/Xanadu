namespace Xanadu.Host;

/// <summary>Window size and the <c>--shot</c> settings every host exe takes.</summary>
internal sealed record HostWindowOptions(uint Width, uint Height, string? ShotPath, int ShotFrame);
