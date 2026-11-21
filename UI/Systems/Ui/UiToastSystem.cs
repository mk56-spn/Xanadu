using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Core.Tweens;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// General-purpose timed toast. Showing one plays its whole life as tweens at
/// once: a pop in (fading in, rising into place), then, after its time up, a
/// fade out (fading, drifting up). The toast's place and colours never change;
/// the tweens fade and move it from there.
/// </summary>
public sealed class ToastSystem : BaseSystem
{
    private const float PopInSeconds = 0.16f;
    private const float FadeOutSeconds = 0.28f;
    private const float DefaultDuration = 1.8f;
    private const int FontSize = 16;
    private const int SmallFontSize = 13;
    private const float Height = 42f;
    private const float PaddingX = 24f;
    private const float MinWidth = 220f;
    private const float ScreenMargin = 16f;

    /// <summary>How far below its place it pops in from, and how far above it it leaves to.</summary>
    private const float PopRise = 10f;
    private const float LeaveRise = 8f;

    /// <summary>Extra time on screen per character, so a long message stays long enough to read.</summary>
    private const float SecondsPerCharacter = 0.05f;
    private static readonly Color PanelColor = new(0.03f, 0.04f, 0.08f, 0.6f);
    private static readonly Color TextColor = new(Palette.TextBright, 0.8f);

    private readonly Queue<(string Message, float Duration)> _queue = new();

    /// <summary>The latest status waiting to replace the toast on the next update.</summary>
    private string? _status;
    private readonly Vector2 _home;
    private readonly float _maxWidth;
    private readonly Entity _toast;
    private float _gone;
    private bool _active;

    /// <summary>The toast's entity: its panel and text.</summary>
    public Entity Entity => _toast;

    /// <summary>Whether a message is up, popping in, showing or fading out.</summary>
    public bool Active => _active;

    public ToastSystem(
        EntityStore entities,
        Vector2 screen,
        IVisualItemFactory visuals)
    {
        _home = new Vector2(screen.X / 2, 100);
        _maxWidth = Math.Max(MinWidth, screen.X - ScreenMargin * 2);

        UiBuilder ui = UiBuilder.Begin(entities, visuals).Layer(UiLayers.Toast);
        _toast = ui.RectEntity(
            _home,
            new Vector2(MinWidth, Height),
            PanelColor,
            "");
        _toast.AddComponent(new TextStyle(FontSize, TextColor));
        UiTween.Target(_toast);
        UiLayout.SetHidden(_toast, true);
    }

    /// <summary>Queues a message; this system's next update shows it once the one up has gone.</summary>
    public void Show(string message, float duration = DefaultDuration)
    {
        if (_toast.IsNull)
            return;

        _queue.Enqueue((message, duration));
    }

    /// <summary>
    /// Shows an editor's latest action message at once, replacing whatever is
    /// up: a newer status makes the older one stale. It stays up longer the
    /// longer it is, and the toast widens to show all of it. It only records
    /// the message; this system's next update shows it, so callers may be
    /// inside a query loop, where showing it would change the store's structure.
    /// </summary>
    public void ShowStatus(string message)
    {
        if (_toast.IsNull || string.IsNullOrWhiteSpace(message))
            return;

        _queue.Clear();
        _status = message;
    }

    /// <summary>How long a message stays up: the default, plus time for a long one to be read.</summary>
    public static float ReadingTime(string message) =>
        Math.Max(DefaultDuration, message.Length * SecondsPerCharacter);

    protected override void OnUpdateGroup()
    {
        if (_toast.IsNull)
            return;

        if (_status is { } status)
        {
            _status = null;
            Begin(status, ReadingTime(status));
        }
        else if (!_active)
        {
            if (_queue.Count == 0)
                return;

            (string message, float duration) = _queue.Dequeue();
            Begin(message, duration);
        }

        if (ClipClocks.Seconds < _gone)
            return;

        _active = false;
        UiTween.Clear(_toast);
        SetText(string.Empty);
        UiLayout.SetHidden(_toast, true);
    }

    /// <summary>Puts the message up and plays its whole life: in now, out once its time is up.</summary>
    private void Begin(string message, float duration)
    {
        UiLayout.SetHidden(_toast, false);
        _active = true;
        Fit(message);
        SetText(message);

        Clip popIn = Clip.FromNow(PopInSeconds);
        Clip fadeOut = popIn.Then(Math.Max(0.1f, duration)).Then(FadeOutSeconds);
        _gone = fadeOut.End;
        UiTween.Clear(_toast);
        UiTween.Play(_toast, Tween.FadeOpacityIn(0f, popIn));
        UiTween.MoveIn(_toast, new Vector2(0, PopRise), popIn);
        UiTween.Play(_toast, Tween.FadeOpacity(0f, fadeOut));
        UiTween.MoveBy(_toast, new Vector2(0, -LeaveRise), fadeOut);
    }

    /// <summary>Sizes the toast to the whole message, dropping to a smaller font before it would run off screen.</summary>
    private void Fit(string message)
    {
        int fontSize = FontSize;
        float width = UiNode.Measure(message, fontSize).X + PaddingX * 2;
        if (width > _maxWidth)
        {
            fontSize = SmallFontSize;
            width = UiNode.Measure(message, fontSize).X + PaddingX * 2;
        }

        var size = new Vector2(Math.Clamp(width, MinWidth, _maxWidth), Height);
        _toast.GetComponent<Rectangle>().Bounds = new Rect2(-size / 2, size);
        _toast.GetComponent<TextStyle>().FontSize = fontSize;
    }

    private void SetText(string message)
    {
        _toast.GetComponent<Text>().Value = message;
        _toast.AddTag<Dirty>();
    }
}
