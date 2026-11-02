namespace Xanadu.Systems.Scenes;

/// <summary>
/// How saving the open level went. A struct and not a nullable message, because
/// null meaning success reads as "nothing happened" to <c>?.</c> and <c>??</c>.
/// </summary>
public readonly record struct EditorSaveOutcome
{
    private const string UnknownProblem = "the level could not be saved";

    private EditorSaveOutcome(bool saved, string problem)
    {
        Saved = saved;
        Problem = problem;
    }

    public static EditorSaveOutcome Success { get; } = new(true, "");

    public bool Saved { get; }

    /// <summary>Why it did not save; empty when it did.</summary>
    public string Problem { get; }

    public static EditorSaveOutcome Failed(string problem) =>
        new(false, string.IsNullOrWhiteSpace(problem) ? UnknownProblem : problem);
}
