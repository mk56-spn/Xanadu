using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>General-purpose timed toast with pop-in and fade-out animation.</summary>
public sealed class ToastSystem : QuerySystem<Toast>
{
    private const float PopInSeconds = 0.16f;
    private const float FadeOutSeconds = 0.28f;
    private const float DefaultDuration = 1.8f;
    private static readonly Vector2 HiddenPosition = new(-10_000, -10_000);
    private static readonly Color PanelColor = new(0.03f, 0.04f, 0.08f, 0.94f);
    private static readonly Color TextColor = Palette.TextBright;

    private readonly Queue<string> _queue = new();
    private readonly Vector2 _home;
    private readonly Entity _toast;

    public ToastSystem(
        EntityStore entities,
        Vector2 screen,
        IVisualRidFactory visuals)
    {
        _home = new Vector2(screen.X / 2, 100);

        UiBuilder ui = UiBuilder.Begin(entities, visuals).Layer(990);
        _toast = ui.RectEntity(
            HiddenPosition,
            new Vector2(620, 42),
            new Color(PanelColor, 0f),
            "");
        _toast.AddComponent(new TextStyle(16, new Color(TextColor, 0f)));
        _toast.AddComponent(new Toast());
    }

    public void Show(string message, float duration = DefaultDuration)
    {
        if (_toast.IsNull)
            return;

        if (_toast.GetComponent<Toast>().Active)
        {
            _queue.Enqueue(message);
            return;
        }

        Begin(message, duration);
    }

    protected override void OnUpdate()
    {
        if (_toast.IsNull || !_toast.HasComponent<Toast>())
            return;

        ref Toast toast = ref _toast.GetComponent<Toast>();
        CommandBuffer buffer = CommandBuffer;

        if (!toast.Active)
        {
            if (_queue.Count == 0)
                return;

            Begin(_queue.Dequeue(), DefaultDuration);
        }

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
            SetVisual(string.Empty, HiddenPosition, 0f);
            Mark(buffer);
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
        ref Toast toast = ref _toast.GetComponent<Toast>();
        toast.Message = message;
        toast.Age = 0f;
        toast.Duration = Math.Max(0.1f, duration);
        toast.Active = true;
        SetVisual(message, _home, 0f);
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
