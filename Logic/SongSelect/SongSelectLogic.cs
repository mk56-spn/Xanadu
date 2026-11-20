using System;
using System.Collections.Generic;
using Xanadu.Logic.Screens;
using Xanadu.Model.Levels;
using Xanadu.Model.Profiles;

namespace Xanadu.Logic.SongSelect;

/// <summary>Song select's entrances. Step takes how many levels to move by (negative goes back).</summary>
public enum SongSelectEntrance
{
    Play,
    Back,
    Step,
    Edit,
    Delete,
}

/// <summary>What song select asks of the game: start or edit the selected level, confirm a delete, and keep the selection.</summary>
public interface ISongSelectActions
{
    void Play();
    void Edit();
    void AskDelete();

    /// <summary>The selection moved; the game keeps it for when the player comes back.</summary>
    void Selected(int index, LevelEntry? entry);
}

/// <summary>
/// The level catalog and which level is picked. Layouts show the catalog however
/// they like and move the selection through <see cref="SongSelectEntrance.Step"/>;
/// the arrow keys reach it under any layout, so a layout needn't bind it.
/// </summary>
public sealed class SongSelectLogic(
    IReadOnlyList<LevelEntry> catalog,
    int selected,
    ProfileStore profiles,
    IScreenNavigator navigator,
    ISongSelectActions actions) : IScreenLogic<SongSelectEntrance>
{
    public ScreenContract<SongSelectEntrance> Contract { get; } = new ScreenContract<SongSelectEntrance>()
        .Mandatory(SongSelectEntrance.Play)
        .Mandatory(SongSelectEntrance.Back)
        .Optional(SongSelectEntrance.Step)
        // The right-click level menu offers both too.
        .Optional(SongSelectEntrance.Edit)
        .Optional(SongSelectEntrance.Delete);

    public IReadOnlyList<LevelEntry> Catalog => catalog;

    public int Selected { get; private set; } = catalog.Count == 0 ? 0 : Math.Clamp(selected, 0, catalog.Count - 1);

    public LevelEntry? SelectedEntry => catalog.Count == 0 ? null : catalog[Selected];

    /// <summary>Goes up whenever what a layout shows may have changed.</summary>
    public int Version { get; private set; }

    /// <summary>The player's profile, for their card and their scores. Read it; don't change it.</summary>
    public ProfileStore Profiles => profiles;

    /// <summary>A level that isn't downloaded yet can be neither played nor mapped.</summary>
    public bool CanPlay => SelectedEntry is { IsBlocked: false };

    public bool CanEdit => SelectedEntry is null || !SelectedEntry.IsBlocked;

    public bool CanDelete => SelectedEntry is { IsCatalogOnly: false };

    public void Do(SongSelectEntrance entrance, int arg)
    {
        switch (entrance)
        {
            case SongSelectEntrance.Play when CanPlay:
                actions.Play();
                break;
            case SongSelectEntrance.Edit when CanEdit:
                actions.Edit();
                break;
            case SongSelectEntrance.Delete when CanDelete:
                actions.AskDelete();
                break;
            case SongSelectEntrance.Step:
                Step(arg);
                break;
            case SongSelectEntrance.Back:
                navigator.Back();
                break;
        }
    }

    /// <summary>Moves the selection by <paramref name="delta"/> levels; false when it can't move.</summary>
    public bool Step(int delta)
    {
        if (catalog.Count == 0)
            return false;

        int next = Math.Clamp(Selected + delta, 0, catalog.Count - 1);
        if (next == Selected)
            return false;

        Selected = next;
        Version++;
        actions.Selected(next, catalog[next]);
        return true;
    }
}
