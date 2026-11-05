using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>Share of the exit's fade its white flash lasts.</summary>
    private const float ExitFlashShare = 0.35f;

    /// <summary>
    /// Draws what is left of a hit dash note: the connector fading from its faint
    /// strength, and the exit jumping to full, flashing, then fading just after it.
    /// </summary>
    public static void DrawDashLinkFade(IRectCanvas canvas, Rid rid, Entity entity)
    {
        DashLinkFade fade = entity.GetComponent<DashLinkFade>();

        float connector = 1f - Math.Clamp(fade.Age / DashLinkFade.ConnectorLifetime, 0f, 1f);
        if (connector > 0f)
        {
            DrawDashConnector(
                canvas, rid, fade.From, fade.To, fade.Radius,
                Tinted(fade.Color, DashConnectorAlpha * connector));
        }

        float t = Math.Clamp(fade.Age / DashLinkFade.ExitLifetime, 0f, 1f);
        float exit = 1f - t * t;
        DrawDashExit(canvas, rid, fade.To, fade.Radius, Tinted(fade.Color, exit));

        if (t < ExitFlashShare)
        {
            float flash = 1f - t / ExitFlashShare;
            canvas.DrawCircle(rid, fade.To, fade.Radius, new Color(1f, 1f, 1f, 0.6f * flash * flash));
        }
    }
}
