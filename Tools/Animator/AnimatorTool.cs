using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Writer.Systems.Layout;
using Writer.Systems.Rendering;
using Writer.Systems.Ui;

namespace Writer.Tools.Animator;

/// <summary>Standalone XAF skeleton/animation authoring tool shell.</summary>
public partial class AnimatorTool : Control
{
    private const string DefaultPath = "res://Animations/Characters/contact.xaf";

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private GodotRectCanvas _canvas = null!;
    private ToolPointer _pointer = null!;
    private Rid _preview;
    private Rid _timelineCanvas;
    private bool _wasTimelinePressed;
    private XafPackage _package = null!;
    private AnimatorRuntime _runtime = null!;
    private Entity _status;
    private Entity _autoKey;
    private Entity _timeline;
    private bool _autoKeyEnabled;
    private float _timeScale = 1f;

    public override void _Ready()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _canvas = new GodotRectCanvas();
        _pointer = new ToolPointer(this);
        _preview = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_preview, GetCanvasItem());
        RenderingServer.CanvasItemSetZIndex(_preview, 1);

        _timelineCanvas = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_timelineCanvas, GetCanvasItem());
        RenderingServer.CanvasItemSetZIndex(_timelineCanvas, 4);

        _root.Add(new LayerParentSystem(GetCanvasItem()));
        _root.Add(new ContainerLayoutSystem());
        _root.Add(new UiAnchorSystem());
        _root.Add(new DrawableInitialiseSystem());
        _root.Add(new TransformSystem(_canvas));
        _root.Add(new RectDrawSystem(_canvas));
        _root.Add(new TextDrawSystem(_canvas));
        _root.Add(new InputSystem(_pointer));
        _root.Add(new ButtonVisualSystem());
        _root.Add(new ToolActionSystem(this));

        _package = LoadPackage();
        _runtime = new AnimatorRuntime(_package);
        _runtime.Play("idle", 0.15f);
        BuildUi();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta * _timeScale;
        _runtime.Update(dt);
        _root.Update(new UpdateTick((float)delta, (float)Time.GetTicksMsec() / 1000f));
        HandleTimelineInput();
        DrawPreview();
        DrawTimeline();
    }

    private void BuildUi()
    {
        Vector2 screen = GetViewportRect().Size;
        UiBuilder ui = UiBuilder.Begin(_store).Layer(0);
        ui.RectEntity(screen / 2, screen, new Color("#090d16"));

        HudText(ui, new Vector2(220, 26), new Vector2(360, 28),
            "XAF ANIMATOR", 24, new Color("#facc15"));
        HudText(ui, new Vector2(220, 52), new Vector2(520, 18),
            "skeleton / clips / rigid SVG groups", 12, new Color("#94a3b8"));

        _status = HudText(ui, new Vector2(520, 50), new Vector2(420, 18),
            "", 12, new Color("#a3e635"));

        BuildButton(ui, "PLAY", ToolActionKind.Play,
            new Vector2(980, 30), new Vector2(80, 30));
        BuildButton(ui, "STOP", ToolActionKind.Stop,
            new Vector2(1068, 30), new Vector2(80, 30));
        _autoKey = BuildButton(ui, "AUTO KEY OFF", ToolActionKind.AutoKey,
            new Vector2(980, 68), new Vector2(168, 30));
        BuildButton(ui, "SAVE XAF", ToolActionKind.Save,
            new Vector2(1200, 30), new Vector2(168, 30));
        BuildButton(ui, "LOAD XAF", ToolActionKind.Load,
            new Vector2(1200, 68), new Vector2(168, 30));

        float left = 20;
        float top = 100;
        float bottom = screen.Y - 150;
        ui.RectEntity(new Vector2(left + 120, (top + bottom) / 2),
            new Vector2(220, bottom - top), new Color("#111827"));
        HudText(ui, new Vector2(left + 120, top + 20), new Vector2(190, 20),
            "BONES", 14, new Color("#facc15"));
        for (int i = 0; i < _package.Skeleton.Bones.Count; i++)
        {
            XafBone bone = _package.Skeleton.Bones[i];
            HudText(ui, new Vector2(left + 120, top + 52 + i * 24),
                new Vector2(190, 20),
                $"{bone.Id}  z{bone.ZOrder}", 13, new Color("#cbd5e1"));
        }

        float rightX = screen.X - 140;
        ui.RectEntity(new Vector2(rightX, (top + bottom) / 2),
            new Vector2(240, bottom - top), new Color("#111827"));
        HudText(ui, new Vector2(rightX, top + 20), new Vector2(210, 20),
            "CLIPS", 14, new Color("#facc15"));
        for (int i = 0; i < _package.Clips.Count; i++)
        {
            XafClip clip = _package.Clips[i];
            BuildButton(ui, clip.Id.ToUpperInvariant(), ToolActionKind.Clip,
                new Vector2(rightX, top + 52 + i * 36),
                new Vector2(200, 28), clip.Id);
        }

        _timeline = ui.RectEntity(
            new Vector2(screen.X / 2, screen.Y - 80),
            new Vector2(screen.X - 80, 90),
            new Color("#111827"));
        HudText(ui, new Vector2(screen.X / 2, screen.Y - 112),
            new Vector2(500, 18), "TIMELINE", 12, new Color("#94a3b8"));
    }

    private Entity BuildButton(
        UiBuilder ui,
        string label,
        ToolActionKind action,
        Vector2 position,
        Vector2 size,
        string argument = "")
    {
        Entity button = ui.RectEntity(position, size,
            new Color("#2563eb"), label, takesInput: true);
        button.AddComponent(new TextStyle(13, new Color("#e2e8f0")));
        button.AddComponent(ButtonVisual.Create(new Color("#2563eb")));
        button.AddComponent(new ToolAction(action, argument));
        return button;
    }

    private Entity HudText(
        UiBuilder ui,
        Vector2 position,
        Vector2 size,
        string text,
        int fontSize,
        Color color)
    {
        Entity label = ui.RectEntity(position, size,
            new Color(0, 0, 0, 0), text);
        label.AddComponent(new TextStyle(fontSize, color));
        return label;
    }

    private void DrawPreview()
    {
        _canvas.Clear(_preview);
        Vector2 screen = GetViewportRect().Size;
        var area = new Rect2(250, 100, screen.X - 560, screen.Y - 270);
        _canvas.DrawRect(_preview, area, new Color("#0f172a"));
        _canvas.DrawLine(_preview,
            new Vector2(area.Position.X, area.End.Y),
            new Vector2(area.End.X, area.End.Y),
            new Color("#334155"), 1);

        Transform2D[] world = XafRig.WorldBones(_package, _runtime.Pose);
        Vector2 center = area.Position + area.Size / 2;
        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = _package.Skeleton.Bones[i];
            Vector2 point = center + world[i].Origin;
            _canvas.DrawCircle(_preview, point, 5, new Color("#facc15"));
            if (bone.Parent >= 0 && bone.Parent < world.Length)
            {
                Vector2 parent = center + world[bone.Parent].Origin;
                _canvas.DrawLine(_preview, parent, point,
                    new Color("#38bdf8"), 2);
            }
            _canvas.DrawTextStyled(
                _preview,
                new Rect2(point + new Vector2(8, -12), new Vector2(100, 20)),
                bone.Id,
                new Color("#cbd5e1"),
                12);
        }

        Transform2D[] attachments = XafRig.WorldAttachments(
            _package, _runtime.Pose, world);
        for (int i = 0; i < attachments.Length; i++)
        {
            Vector2 point = center + attachments[i].Origin;
            _canvas.DrawRect(_preview,
                new Rect2(point - new Vector2(5, 5), new Vector2(10, 10)),
                new Color("#a3e635"));
        }

        _canvas.DrawTextStyled(
            _preview,
            new Rect2(area.Position + new Vector2(12, 12), new Vector2(300, 20)),
            $"{_runtime.CurrentClip}  {_runtime.CurrentTime:0.00}s",
            new Color("#e2e8f0"), 14);
    }

    private Rect2 TimelineRect()
    {
        Vector2 screen = GetViewportRect().Size;
        return new Rect2(40, screen.Y - 118, screen.X - 80, 56);
    }

    private void HandleTimelineInput()
    {
        PointerInput input = _pointer.Read();
        Rect2 bar = TimelineRect();
        if (input.Pressed && !_wasTimelinePressed && bar.HasPoint(input.Position))
        {
            float t = Mathf.Clamp(
                (input.Position.X - bar.Position.X) / Mathf.Max(1f, bar.Size.X),
                0f,
                1f);
            _runtime.Seek(_runtime.CurrentDuration * t);
            _runtime.SetPaused(true);
        }
        _wasTimelinePressed = input.Pressed;
    }

    private void DrawTimeline()
    {
        _canvas.Clear(_timelineCanvas);
        Rect2 bar = TimelineRect();
        _canvas.DrawRect(
            _timelineCanvas,
            new Rect2(bar.Position.X, bar.Position.Y + 18,
                bar.Size.X, 2),
            new Color("#475569"));

        float duration = Math.Max(0.1f, _runtime.CurrentDuration);
        float t = Mathf.Clamp(_runtime.CurrentTime / duration, 0f, 1f);
        float x = Mathf.Lerp(bar.Position.X, bar.End.X, t);
        _canvas.DrawLine(
            _timelineCanvas,
            new Vector2(x, bar.Position.Y + 8),
            new Vector2(x, bar.Position.Y + 30),
            new Color("#facc15"),
            2);

        XafClip? clip = null;
        foreach (XafClip candidate in _package.Clips)
            if (candidate.Id == _runtime.CurrentClip)
                clip = candidate;
        if (clip is null)
            return;

        foreach (XafChannel channel in clip.Channels)
        {
            foreach (XafKeyframe key in channel.Keyframes)
            {
                float keyX = Mathf.Lerp(
                    bar.Position.X,
                    bar.End.X,
                    Mathf.Clamp(key.Time / duration, 0f, 1f));
                _canvas.DrawCircle(
                    _timelineCanvas,
                    new Vector2(keyX, bar.Position.Y + 19), 4,
                    new Color("#38bdf8"));
            }
        }
    }

    private XafPackage LoadPackage()
    {
        string path = ProjectSettings.GlobalizePath(DefaultPath);
        if (System.IO.File.Exists(path))
        {
            try
            {
                return XafBinary.Load(path);
            }
            catch (Exception)
            {
                // Fall through to the default package.
            }
        }

        return CreateDefaultPackage();
    }

    private static XafPackage CreateDefaultPackage()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
            Rest = Transform2D.Identity,
        });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "torso",
            Parent = 0,
            Rest = new Transform2D(0f, new Vector2(0, -80)),
        });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "head",
            Parent = 1,
            Rest = new Transform2D(0f, new Vector2(0, -70)),
        });
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "arm",
            Parent = 1,
            Rest = new Transform2D(0f, new Vector2(55, -40)),
        });
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://Assets/SVG/body.svg",
            Group = "body",
            BoneId = "torso",
            Transform = new Transform2D(0f, new Vector2(0, -20)),
        });
        package.Clips.Add(Clip("idle", 0f, 8f, 2f, 0f));
        package.Clips.Add(Clip("jumping", 0f, 20f, 1.2f, 1f));
        package.Clips.Add(Clip("landing", 20f, 0f, 1.2f, 0f));
        package.Clips.Add(Clip("death", 0f, -20f, 2f, 0f));
        return package;
    }

    private static XafClip Clip(
        string id,
        float start,
        float end,
        float duration,
        float loopEnd)
    {
        var clip = new XafClip { Id = id, Duration = duration };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "torso",
            Property = XafChannelProperty.TranslationY,
        };
        channel.Keyframes.Add(new XafKeyframe { Time = 0f, Value = start });
        channel.Keyframes.Add(new XafKeyframe { Time = duration, Value = end });
        clip.Channels.Add(channel);
        return clip;
    }

    private void SavePackage()
    {
        string path = ProjectSettings.GlobalizePath(DefaultPath);
        XafBinary.Save(path, _package);
        SetStatus($"SAVED {DefaultPath}");
    }

    private void SetStatus(string text)
    {
        if (_status.HasComponent<Text>())
            _status.GetComponent<Text>().Value = text;
    }

    private void RunAction(ToolAction action)
    {
        switch (action.Kind)
        {
            case ToolActionKind.Play:
                _runtime.SetPaused(false);
                _timeScale = 1f;
                break;
            case ToolActionKind.Stop:
                _runtime.SetPaused(true);
                break;
            case ToolActionKind.AutoKey:
                _autoKeyEnabled = !_autoKeyEnabled;
                _autoKey.GetComponent<Text>().Value =
                    _autoKeyEnabled ? "AUTO KEY ON" : "AUTO KEY OFF";
                break;
            case ToolActionKind.Save:
                SavePackage();
                break;
            case ToolActionKind.Load:
                _package = LoadPackage();
                _runtime = new AnimatorRuntime(_package);
                _runtime.Play("idle", 0.15f);
                SetStatus("LOADED");
                break;
            case ToolActionKind.Clip:
                _runtime.Play(action.Argument, 0.2f);
                _runtime.SetPaused(false);
                break;
        }
    }

    private sealed class ToolActionSystem(AnimatorTool owner)
        : QuerySystem<ToolAction>
    {
        protected override void OnUpdate()
        {
            foreach (Entity entity in Query.ToEntityList())
            {
                if (!entity.Tags.Has<Pressed>())
                    continue;
                owner.RunAction(entity.GetComponent<ToolAction>());
                entity.RemoveTag<Pressed>();
            }
        }
    }

    private sealed class ToolPointer(AnimatorTool owner) : IPointerInputSource
    {
        public PointerInput Read() => new(
            owner.GetViewport().GetMousePosition(),
            Input.IsMouseButtonPressed(MouseButton.Left));
    }

    public enum ToolActionKind
    {
        Play,
        Stop,
        AutoKey,
        Save,
        Load,
        Clip,
    }

    public struct ToolAction(ToolActionKind kind, string argument = "") : IComponent
    {
        public ToolActionKind Kind = kind;
        public string Argument = argument;
    }
}
