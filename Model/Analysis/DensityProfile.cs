namespace Xanadu.Model.Analysis;

/// <summary>
///     How busy a chart is over time: notes per second, and how far the player
///     must travel per second to reach them. Each array is one value per
///     <see cref="BucketMs" /> of song time.
/// </summary>
public sealed record DensityProfile(
    int BucketMs,
    float[] NotesPerSecond,
    float[] TravelPixelsPerSecond,
    float PeakNotes,
    float PeakTravel)
{
    public static readonly DensityProfile Empty = new(500, [], [], 0, 0);

    public int BucketCount => NotesPerSecond.Length;
}
