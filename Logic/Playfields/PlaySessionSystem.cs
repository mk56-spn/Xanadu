using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Rendering;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Audio;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Runs the 3-2-1-GO countdown, starts play-mode audio, fades it out after the
/// final note, and marks the session finished so the scene can move to results.
/// </summary>
public sealed class PlaySessionSystem : QuerySystem<PlaySession, Playhead>
{
    private const float BaseVolume = 0.8f;
    private const float CountdownSeconds = 3f;
    private const float GoSeconds = 0.6f;

    /// <summary>How long a death holds before the level restarts from its start.</summary>
    private const float DeathSeconds = 0.8f;

    private readonly ITrackAudio _audio;

    public PlaySessionSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.EachEntity(new Run(this, Query.Store, CommandBuffer, Tick.deltaTime));
    }

    private void UpdateCountdown(
        ref PlaySession session,
        ref Playhead playhead,
        CommandBuffer buffer)
    {
        if (session.CountdownElapsed <= 0f)
            SetStatus(ref session, "GET READY", buffer);

        session.CountdownElapsed += Tick.deltaTime;

        if (session.CountdownElapsed < CountdownSeconds)
        {
            int number = 3 - (int)session.CountdownElapsed;
            SetCountdown(ref session, number.ToString(), buffer);
            return;
        }

        session.CountdownDone = true;
        ShaderPlay.Mark(ShaderEvent.CountdownDone);
        SetCountdown(ref session, "GO!", buffer);

        if (playhead.DurationMs > 0)
        {
            StartPlayback(ref session, ref playhead, buffer);
        }
        else
        {
            SetStatus(ref session, "AUDIO UNAVAILABLE", buffer);
        }
    }

    /// <summary>
    /// Ends a run killed by a spike: the music cuts immediately, the status says
    /// DEAD, and after a short beat the session finishes so the scene can send
    /// the player back to the start.
    /// </summary>
    private void UpdateDeath(
        ref PlaySession session,
        ref Playhead playhead,
        CommandBuffer buffer)
    {
        if (!session.Ending)
        {
            session.Ending = true;
            session.FadeElapsed = 0f;
            session.FadeDuration = DeathSeconds;
            playhead.Playing = false;
            _audio.SetVolume(0f);
            _audio.Pause();
            SetStatus(ref session, "DEAD", buffer);
            return;
        }

        session.FadeElapsed += Tick.deltaTime;
        if (session.FadeElapsed < session.FadeDuration)
            return;

        session.Finished = true;
        SetStatus(ref session, "DEAD", buffer);
    }

    private static void StartPlayback(
        ref PlaySession session,
        ref Playhead playhead,
        CommandBuffer buffer)
    {
        playhead.SeekRequestMs = session.StartTimeMs;
        playhead.Playing = true;
        session.Started = true;
        ShaderPlay.Mark(ShaderEvent.SongStart);
        SetStatus(ref session, "PLAYING", buffer);
    }

    private void HideCountdownAfterGo(
        ref PlaySession session,
        CommandBuffer buffer)
    {
        if (session.CountdownElapsed >= CountdownSeconds + GoSeconds)
        {
            SetCountdown(ref session, "", buffer);
            return;
        }

        session.CountdownElapsed += Tick.deltaTime;

        if (session.CountdownElapsed >= CountdownSeconds + GoSeconds)
            SetCountdown(ref session, "", buffer);
    }

    private static void SetStatus(
        ref PlaySession session,
        string text,
        CommandBuffer buffer)
    {
        if (session.Status.IsNull ||
            !session.Status.HasComponent<Text>())
        {
            return;
        }

        ref Text status = ref session.Status.GetComponent<Text>();
        if (status.Value == text)
            return;

        status.Value = text;
        buffer.AddTag<Dirty>(session.Status.Id);
    }

    private static void SetCountdown(
        ref PlaySession session,
        string text,
        CommandBuffer buffer)
    {
        if (session.Countdown.IsNull ||
            !session.Countdown.HasComponent<Text>())
        {
            return;
        }

        ref Text countdown = ref session.Countdown.GetComponent<Text>();
        if (countdown.Value == text)
            return;

        countdown.Value = text;
        buffer.AddTag<Dirty>(session.Countdown.Id);
    }

    private readonly record struct Run(PlaySessionSystem System, EntityStore Store, CommandBuffer Buffer, float Delta) : IEachEntity<PlaySession, Playhead>
    {
        public void Execute(ref PlaySession session, ref Playhead playhead, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (entity.Tags.Has<PlayPaused>())
                return;

            if (session.Finished)
                return;

            if (session.Died)
            {
                System.UpdateDeath(ref session, ref playhead, Buffer);
                return;
            }

            if (!session.CountdownDone)
            {
                System.UpdateCountdown(ref session, ref playhead, Buffer);
                return;
            }

            if (!session.Started)
            {
                if (playhead.DurationMs > 0)
                {
                    StartPlayback(ref session, ref playhead, Buffer);
                }
                else
                {
                    System.HideCountdownAfterGo(ref session, Buffer);
                }

                return;
            }

            session.Elapsed += Delta;
            System.HideCountdownAfterGo(ref session, Buffer);

            bool hasEnd = session.LastNoteMs >= 0 || playhead.DurationMs > 0;
            long endMs = session.LastNoteMs >= 0
                ? session.LastNoteMs
                : playhead.DurationMs;
            bool audioEnded = playhead.DurationMs > 0 &&
                              !playhead.Playing &&
                              playhead.TimeMs >= playhead.DurationMs - 1;

            if (!session.Ending &&
                hasEnd &&
                (playhead.TimeMs >= endMs || audioEnded))
            {
                session.Ending = true;
                session.FadeElapsed = 0f;
                SetStatus(ref session, "FINISHING", Buffer);
            }

            if (!session.Ending)
                return;

            session.FadeElapsed += Delta;
            float fade = Math.Clamp(
                session.FadeElapsed / Math.Max(0.01f, session.FadeDuration),
                0f,
                1f);

            System._audio.SetVolume(BaseVolume * (1f - fade));

            if (fade < 1f)
                return;

            session.Finished = true;
            playhead.Playing = false;
            System._audio.SetVolume(0f);
            System._audio.Pause();
            SetStatus(ref session, "FINISHED", Buffer);
        }
    }
}
