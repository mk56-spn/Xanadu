using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>General-purpose timed toast with pop-in and fade-out animation.</summary>
public sealed class ToastSystem : QuerySystem<Toast>
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

    public ToastSystem(
        EntityStore entities,
        Vector2 screen,
        IVisualRidFactory visuals)
    {
        _home = new Vector2(screen.X / 2, 100);
        _maxWidth = Math.Max(MinWidth, screen.X - ScreenMargin * 2);

        UiBuilder ui = UiBuilder.Begin(entities, visuals).Layer(UiLayers.Toast);
        _toast = ui.RectEntity(
            Vector2.Zero,
            new Vector2(MinWidth, Height),
            new Color(PanelColor, 0f),
            "");
        _toast.AddComponent(new TextStyle(FontSize, new Color(TextColor, 0f)));
        _toast.AddComponent(new Toast());
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

    protected override void OnUpdate()
    {
        if (_toast.IsNull || !_toast.HasComponent<Toast>())
            return;

        CommandBuffer buffer = CommandBuffer;
        if (_status is { } status)
        {
            _status = null;
            Begin(status, ReadingTime(status));
        }
        else if (!_toast.GetComponent<Toast>().Active)
        {
            if (_queue.Count == 0)
                return;

            (string message, float duration) = _queue.Dequeue();
            Begin(message, duration);
        }

        // Showing the toast moved its components, so take the reference after it.
        ref Toast toast = ref _toast.GetComponent<Toast>();
        toast.Age += Tick.deltaTime;

        float total = PopInSeconds + toast.Duration + FadeOutSeconds;
        float alpha;
        float offsetY;

        if (toast.Age < PopInSeconds)
        {
            float t = toast.Age / PopInSeconds;
            alpha = t;
            offsetY = (1f - t) * 10f;
        }
        else if (toast.Age < PopInSeconds + toast.Duration)
        {
            alpha = 1f;
            offsetY = 0f;
        }
        else if (toast.Age < total)
        {
            float t = (toast.Age - PopInSeconds - toast.Duration) /
                      FadeOutSeconds;
            alpha = 1f - t;
            offsetY = -t * 8f;
        }
        else
        {
            toast.Active = false;
            SetVisual(string.Empty, _home, 0f);
            Mark(buffer);
            UiLayout.SetHidden(_toast, buffer, true);
            return;
        }

        SetVisual(
            toast.Message,
            _home + new Vector2(0, offsetY),
            alpha);
        Mark(buffer);
    }

    private void Begin(string message, float duration)
    {
        UiLayout.SetHidden(_toast, false);
        ref Toast toast = ref _toast.GetComponent<Toast>();
        toast.Message = message;
        toast.Age = 0f;
        toast.Duration = Math.Max(0.1f, duration);
        toast.Active = true;
        Fit(message);
        SetVisual(message, _home, 0f);
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

    private void SetVisual(string message, Vector2 position, float alpha)
    {
        ref Text text = ref _toast.GetComponent<Text>();
        text.Value = message;

        ref Position pos = ref _toast.GetComponent<Position>();
        pos.Value = position;

        ref FillColor fill = ref _toast.GetComponent<FillColor>();
        fill.Value = new Color(PanelColor, PanelColor.A * alpha);

        ref TextStyle style = ref _toast.GetComponent<TextStyle>();
        style.Color = new Color(TextColor, TextColor.A * alpha);
    }

    private void Mark(CommandBuffer buffer)
    {
        buffer.AddTag<Dirty>(_toast.Id);
        buffer.AddTag<Moved>(_toast.Id);
    }
}
