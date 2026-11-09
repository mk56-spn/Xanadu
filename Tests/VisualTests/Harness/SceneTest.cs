using System;
using System.Collections.Generic;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Small osu!framework-style test scene: build a sequence of steps, then let
/// the runner execute them one at a time against a live ECS context.
/// </summary>
public abstract class SceneTest
{
    private readonly List<SceneTestStep> _steps = new();

    protected SceneTest()
    {
        SetUpSteps();
    }

    public abstract string Name { get; }

    public abstract string Slug { get; }

    public IReadOnlyList<SceneTestStep> Steps => _steps;

    protected VisualTestContext Context { get; private set; } = null!;

    protected abstract void SetUpSteps();

    internal void Attach(VisualTestContext context)
    {
        Context = context;
        ResetSteps();
    }

    internal void ResetSteps()
    {
        foreach (SceneTestStep step in _steps)
            step.Reset();
    }

    protected void AddStep(string description, Action action) =>
        _steps.Add(new ActionTestStep(description, action));

    protected void AddUntilStep(
        string description,
        Func<bool> condition,
        double timeoutSeconds = 10) =>
        _steps.Add(new UntilTestStep(description, condition, timeoutSeconds));

    protected void AddWaitStep(string description, int frames) =>
        _steps.Add(new WaitTestStep(description, frames));

    protected void AddAssert(
        string description,
        Func<bool> assertion,
        string? extendedDescription = null) =>
        _steps.Add(new AssertTestStep(description, assertion, extendedDescription));

    /// <summary>
    /// Gives the open editor's level the test track. A level without a song
    /// holds the song picker up, and its scrim takes every press, so a test that
    /// works the editor's UI adds this once the editor is up.
    /// </summary>
    protected void AddEditorSong()
    {
        AddStep("give the level the test track", () =>
        {
            foreach (Friflo.Engine.ECS.Entity editor in Context.Entities.Query<Xanadu.Ecs.LevelInfo>().ToEntityList())
                editor.GetComponent<Xanadu.Ecs.LevelInfo>().Song = TestContent.Song("visual-editor", "Visual Editor");
        });
        AddUntilStep("the song picker is down", () =>
        {
            if (Xanadu.Ecs.EditorDialogs.IsSongRequired(Context.Entities))
                return false;

            Xanadu.Ecs.EditorDialogs.Dismiss(Context.Entities);
            return !Xanadu.Ecs.EditorDialogs.AnyOpen(Context.Entities);
        });
    }

    protected void AddRepeatStep(string description, Action action, int count)
    {
        for (int i = 0; i < count; i++)
            AddStep($"{description} ({i + 1}/{count})", action);
    }
}
