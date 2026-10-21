using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>Starts play-mode audio once loaded and reports play-session status.</summary>
public sealed class PlaySessionSystem : QuerySystem<PlaySession, EditorPlayhead>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref session, ref playhead, _) =>
        {
            if (!session.Started)
            {
                session.Elapsed += Tick.deltaTime;

                if (playhead.DurationMs > 0)
                {
                    playhead.SeekRequestMs = 0;
                    playhead.Playing = true;
                    session.Started = true;
                    SetStatus(ref session, "PLAYING", buffer);
                }
                else if (session.Elapsed > 2f)
                {
                    SetStatus(ref session, "AUDIO UNAVAILABLE", buffer);
                }

                return;
            }

            if (!session.Finished &&
                !playhead.Playing &&
                playhead.DurationMs > 0 &&
                playhead.TimeMs >= playhead.DurationMs - 1)
            {
                session.Finished = true;
                SetStatus(
                    ref session,
                    "FINISHED - BACK TO SONG SELECT",
                    buffer);
            }
        });
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
}
