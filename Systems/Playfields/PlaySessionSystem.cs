using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Systems.Audio;

namespace Writer.Systems.Playfields;

/// <summary>
/// Runs the 3-2-1-GO countdown, starts play-mode audio, fades it out after the
/// final note, and marks the session finished so the scene can move to results.
/// </summary>
public sealed class PlaySessionSystem : QuerySystem<PlaySession, Playhead>
{
    private const float BaseVolume = 0.8f;
    private const float CountdownSeconds = 3f;
    private const float GoSeconds = 0.6f;

    private readonly ITrackAudio _audio;

    public PlaySessionSystem(ITrackAudio audio)
    {
        _audio = audio;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref session, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>())
                return;

            if (session.Finished)
                return;

            if (!session.CountdownDone)
            {
                UpdateCountdown(ref session, ref playhead, buffer);
                return;
            }

            if (!session.Started)
            {
                if (playhead.DurationMs > 0)
                {
                    StartPlayback(ref session, ref playhead, buffer);
                }
                else
                {
                    HideCountdownAfterGo(ref session, buffer);
                }

                return;
            }

            session.Elapsed += Tick.deltaTime;
            HideCountdownAfterGo(ref session, buffer);

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
                SetStatus(ref session, "FINISHING", buffer);
            }

            if (!session.Ending)
                return;

            session.FadeElapsed += Tick.deltaTime;
            float fade = Math.Clamp(
                session.FadeElapsed / Math.Max(0.01f, session.FadeDuration),
                0f,
                1f);

            _audio.SetVolume(BaseVolume * (1f - fade));

            if (fade < 1f)
                return;

            session.Finished = true;
            playhead.Playing = false;
            _audio.SetVolume(0f);
            _audio.Pause();
            SetStatus(ref session, "FINISHED", buffer);
        });
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

    private static void StartPlayback(
        ref PlaySession session,
        ref Playhead playhead,
        CommandBuffer buffer)
    {
        playhead.SeekRequestMs = session.StartTimeMs;
        playhead.Playing = true;
        session.Started = true;
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
}
