using Xanadu.Core.Keys;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Judges an orb hold's tail. Letting the ride key go inside the tail's window
/// judges it by the offset and rides on to the tail, leaving on time; letting
/// go earlier misses it and drops off the orb then, with its motion at that
/// instant. Held past the window, it is judged Fair (the body has already left
/// at the tail).
/// </summary>
public sealed class PlayHoldTails(PlayNoteContext context, IKeyboardState keyboard)
{
    public void Update(Entity playfield)
    {
        if (!playfield.HasComponent<CurveBody>())
            return;

        ref CurveBody body = ref playfield.GetComponent<CurveBody>();
        if (!body.HoldOpen)
            return;

        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        Key key = context.Keybinds.Get(body.RideKey);
        bool held = keyboard.IsPressed(key);
        // A release is judged when the key came up, not when this frame read it.
        long at = held ? playhead.TimeMs : playhead.KeyMs(keyboard.ChangedAt(key));
        long judged = at - PlayOffset.TotalMs(playfield, context.Offsets.OffsetMs);
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        long early = body.Ride.EndMs - judged;

        Judgement judgement;
        if (!held)
        {
            judgement = early > window ? Judgement.Miss : ScoreRules.Judge(early);
            if (early > window)
                body.OnOrb = false;
        }
        else if (-early > window)
        {
            judgement = Judgement.Fair;
            early = -window;
        }
        else
            return;

        body.HoldOpen = false;
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        PlayScoring.Apply(ref score, judgement, early);
        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        score.JudgementPosition = view.PlayfieldToScreen(Orbs.At(body.Ride, playhead.ShownMs));
        score.JudgementTimeMs = playhead.ShownMs;
    }
}
