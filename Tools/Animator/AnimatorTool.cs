using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
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
    private AnimatorPlayback _playback = null!;
    private Entity _status;
    private Entity _autoKey;
    private Entity _onionButton;
    private Entity _fileButton;
    private Entity _fileMenu;
    private bool _fileMenuOpen;
    private Entity _previewPanel;
    private Entity _timelinePanel;
    private bool _autoKeyEnabled;
    private bool _onionEnabled;
    private int _selectedKeyChannel = -1;
    private int _selectedKeyIndex = -1;
    private float _timeScale = 1f;
    private int _selectedBone = -1;
    private bool _draggingBone;
    private bool _wasPointerPressed;
    private Vector2 _dragStartPointer;
    private Vector2 _dragStartTranslation;

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
        UiDebugOverlayGroup.Attach(
            _root,
            _canvas,
            GetCanvasItem());
        _root.Add(new ToolActionSystem(this));

        _package = LoadPackage();
        _runtime = new AnimatorRuntime(_package);
        _playback = new AnimatorPlayback(_runtime);
        _runtime.Play("idle", 0.15f);
        BuildUi();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta * _timeScale;
        HandlePreviewInput();
        _playback.Update(dt);
        _root.Update(new UpdateTick((float)delta, (float)Time.GetTicksMsec() / 1000f));
        HandleTimelineInput();
        DrawPreview();
        DrawTimeline();
    }

    private void BuildUi()
    {
        Vector2 screen = GetViewportRect().Size;
        const float margin = 16f;
        const float topHeight = 88f;
        const float bottomHeight = 112f;
        const float gap = 12f;
        float mainHeight = screen.Y - margin * 2 - topHeight - bottomHeight - gap * 2;

        UiBuilder ui = UiBuilder.Begin(_store).Layer(0);
        ui.RectEntity(screen / 2, screen, new Color("#090d16"));

        ui.Container(
            screen / 2,
            screen,
            StackDirection.Vertical,
            spacing: gap,
            padding: margin,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Center);
        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X - margin * 2, topHeight),
            StackDirection.Horizontal,
            spacing: 16,
            color: new Color("#111827"),
            padding: 14,
            cross: StackCrossAlignment.Center);
        ui.Container(
            Vector2.Zero,
            new Vector2(650, topHeight - 28),
            StackDirection.Vertical,
            spacing: 4,
            color: new Color(0, 0, 0, 0),
            padding: 0,
            cross: StackCrossAlignment.Start);
        HudText(ui, Vector2.Zero, new Vector2(620, 28),
            "XAF ANIMATOR", 24, new Color("#facc15"));
        HudText(ui, Vector2.Zero, new Vector2(620, 18),
            "skeleton / clips / rigid SVG groups", 12, new Color("#94a3b8"));
        ui.End();
        _status = HudText(ui, Vector2.Zero, new Vector2(520, 24),
            "", 13, new Color("#a3e635"));
        _fileButton = BuildButton(ui, "FILE ▾", ToolActionKind.ToggleFileMenu,
            Vector2.Zero, new Vector2(96, 30));
        ui.End();

        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X - margin * 2, mainHeight),
            StackDirection.Horizontal,
            spacing: gap,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Center);

        ui.Container(
            Vector2.Zero,
            new Vector2(240, mainHeight),
            StackDirection.Vertical,
            spacing: 8,
            color: new Color("#111827"),
            padding: 12,
            cross: StackCrossAlignment.Start);
        HudText(ui, Vector2.Zero, new Vector2(210, 20),
            "BONES", 14, new Color("#facc15"));
        for (int i = 0; i < _package.Skeleton.Bones.Count; i++)
        {
            XafBone bone = _package.Skeleton.Bones[i];
            HudText(ui, Vector2.Zero, new Vector2(210, 20),
                $"{bone.Id}  z{bone.ZOrder}", 13, new Color("#cbd5e1"),
                TextAlign.Left);
        }
        ui.End();

        float centerWidth = screen.X - margin * 2 - 240 - 300 - gap * 2;
        ui.Container(
            Vector2.Zero,
            new Vector2(centerWidth, mainHeight),
            StackDirection.Vertical,
            spacing: 0,
            color: new Color("#0f172a"),
            padding: 0);
        _previewPanel = ui.Current;
        ui.End();

        ui.Container(
            Vector2.Zero,
            new Vector2(300, mainHeight),
            StackDirection.Vertical,
            spacing: 8,
            color: new Color("#111827"),
            padding: 12,
            cross: StackCrossAlignment.Center);
        BuildButtonRow(ui, new[]
        {
            ("PLAY", ToolActionKind.Play),
            ("STOP", ToolActionKind.Stop),
        }, 168);
        _autoKey = BuildButton(ui, "AUTO KEY OFF", ToolActionKind.AutoKey,
            Vector2.Zero, new Vector2(168, 30));
        _onionButton = BuildButton(ui, "ONION OFF", ToolActionKind.Onion,
            Vector2.Zero, new Vector2(168, 30));
        BuildButtonRow(ui, new[]
        {
            ("ROT -", ToolActionKind.RotateLeft),
            ("ROT +", ToolActionKind.RotateRight),
        }, 168);
        BuildButtonRow(ui, new[]
        {
            ("SCALE -", ToolActionKind.ScaleDown),
            ("SCALE +", ToolActionKind.ScaleUp),
        }, 168);
        BuildButton(ui, "KEY BONE", ToolActionKind.KeyBone,
            Vector2.Zero, new Vector2(168, 30));
        HudText(ui, Vector2.Zero, new Vector2(210, 20),
            "CLIPS", 14, new Color("#facc15"));
        for (int i = 0; i < _package.Clips.Count; i++)
        {
            XafClip clip = _package.Clips[i];
            BuildButton(ui, clip.Id.ToUpperInvariant(), ToolActionKind.Clip,
                Vector2.Zero, new Vector2(200, 28), clip.Id);
        }
        ui.End();

        ui.End();

        ui.Container(
            Vector2.Zero,
            new Vector2(screen.X - margin * 2, bottomHeight),
            StackDirection.Horizontal,
            spacing: 12,
            color: new Color("#111827"),
            padding: 10);
        _timelinePanel = ui.Current;
        ui.End();

        ui.End();

        UiBuilder menuUi = UiBuilder.Begin(_store).Layer(2);
        menuUi.Container(
            new Vector2(-10_000, -10_000),
            new Vector2(180, 82),
            StackDirection.Vertical,
            spacing: 6,
            color: new Color("#111827"),
            padding: 6,
            cross: StackCrossAlignment.Center);
        _fileMenu = menuUi.Current;
        BuildButton(menuUi, "SAVE XAF", ToolActionKind.Save,
            Vector2.Zero, new Vector2(168, 30));
        BuildButton(menuUi, "LOAD XAF", ToolActionKind.Load,
            Vector2.Zero, new Vector2(168, 30));
        menuUi.End();
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

    private void BuildButtonRow(
        UiBuilder ui,
        (string Label, ToolActionKind Kind)[] buttons,
        float width)
    {
        ui.Container(
            Vector2.Zero,
            new Vector2(width, 30),
            StackDirection.Horizontal,
            spacing: 6,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Center);
        foreach ((string label, ToolActionKind kind) in buttons)
        {
            BuildButton(ui, label, kind, Vector2.Zero,
                new Vector2((width - 6) / 2, 30));
        }
        ui.End();
    }

    private Entity HudText(
        UiBuilder ui,
        Vector2 position,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        TextAlign align = TextAlign.Center)
    {
        Entity label = ui.RectEntity(position, size,
            new Color(0, 0, 0, 0), text);
        label.AddComponent(new TextStyle(fontSize, color));
        label.AddComponent(new TextAlignment(align));
        return label;
    }

    private static Rect2 PanelArea(Entity panel)
    {
        if (panel.IsNull ||
            !panel.HasComponent<Rectangle>() ||
            !panel.HasComponent<Position>())
        {
            return new Rect2();
        }

        Vector2 center = UiTransform.WorldPosition(panel, Vector2.Zero);
        Vector2 size = panel.GetComponent<Rectangle>().Bounds.Size;
        return new Rect2(center - size / 2, size);
    }

    private Rect2 PreviewArea() => PanelArea(_previewPanel);

    private void HandlePreviewInput()
    {
        PointerInput input = _pointer.Read();
        Rect2 area = PreviewArea();
        if (input.Pressed && !_wasPointerPressed && area.HasPoint(input.Position))
        {
            Transform2D[] world = XafRig.WorldBones(_package, _runtime.Pose);
            Vector2 center = area.Position + area.Size / 2;
            int hit = -1;
            float best = 16f;
            for (int i = 0; i < world.Length; i++)
            {
                float distance = (center + world[i].Origin)
                    .DistanceTo(input.Position);
                if (distance > best)
                    continue;
                best = distance;
                hit = i;
            }

            if (hit >= 0)
            {
                _selectedBone = hit;
                _runtime.SetPaused(true);
                _draggingBone = true;
                _dragStartPointer = input.Position;
                _dragStartTranslation = _runtime.Pose.Bones[hit].Translation;
                SetStatus($"SELECTED {_package.Skeleton.Bones[hit].Id}");
            }
        }
        else if (input.Pressed && _draggingBone && _selectedBone >= 0)
        {
            Vector2 delta = input.Position - _dragStartPointer;
            XafTransform transform = _runtime.Pose.Bones[_selectedBone];
            transform.Translation = _dragStartTranslation + delta;
            _runtime.Pose.Bones[_selectedBone] = transform;
        }
        else if (!input.Pressed && _wasPointerPressed && _draggingBone)
        {
            _draggingBone = false;
            if (_autoKeyEnabled)
                KeySelectedBone();
        }

        _wasPointerPressed = input.Pressed;
    }

    private XafClip? CurrentClip()
    {
        foreach (XafClip clip in _package.Clips)
            if (clip.Id == _runtime.CurrentClip)
                return clip;
        return null;
    }

    private void KeySelectedBone()
    {
        if (_selectedBone < 0)
            return;

        XafClip? clip = CurrentClip();
        if (clip is null)
            return;

        XafAuthoring.KeyBoneTransform(
            _package,
            clip,
            _selectedBone,
            _runtime.CurrentTime,
            _runtime.Pose.Bones[_selectedBone]);
        SetStatus($"KEYED {_package.Skeleton.Bones[_selectedBone].Id}");
    }

    private void DrawGhost(
        XafClip clip,
        float time,
        Color color,
        Vector2 center)
    {
        float duration = Mathf.Max(0.001f, clip.Duration);
        time = Mathf.Clamp(time, 0f, duration);
        XafPose pose = XafSampler.Sample(_package, clip, time);
        Transform2D[] world = XafRig.WorldBones(_package, pose);
        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = _package.Skeleton.Bones[i];
            Vector2 point = center + world[i].Origin;
            _canvas.DrawCircle(_preview, point, 3, color);
            if (bone.Parent >= 0 && bone.Parent < world.Length)
                _canvas.DrawLine(_preview,
                    center + world[bone.Parent].Origin,
                    point, color, 1);
        }
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

        if (_onionEnabled)
        {
            XafClip? onionClip = CurrentClip();
            if (onionClip is not null)
            {
                DrawGhost(onionClip, _runtime.CurrentTime - 0.1f,
                    new Color("#38bdf8", 0.25f), center);
                DrawGhost(onionClip, _runtime.CurrentTime + 0.1f,
                    new Color("#fb7185", 0.25f), center);
            }
        }

        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = _package.Skeleton.Bones[i];
            Vector2 point = center + world[i].Origin;
            _canvas.DrawCircle(
                _preview,
                point,
                i == _selectedBone ? 8 : 5,
                i == _selectedBone
                    ? Colors.White
                    : new Color("#facc15"));
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
        const float margin = 16f;
        const float bottomHeight = 112f;
        float top = screen.Y - margin - bottomHeight;
        return new Rect2(
            margin + 24,
            top + 28,
            Mathf.Max(1f, screen.X - margin * 2 - 48),
            bottomHeight - 40);
    }

    private void HandleTimelineInput()
    {
        PointerInput input = _pointer.Read();
        Rect2 bar = TimelineRect();
        if (input.Pressed && !_wasTimelinePressed && bar.HasPoint(input.Position))
        {
            XafClip? clip = CurrentClip();
            if (clip is not null)
            {
                float duration = Mathf.Max(0.1f, _runtime.CurrentDuration);
                float best = 10f;
                int bestChannel = -1;
                int bestKey = -1;
                for (int c = 0; c < clip.Channels.Count; c++)
                {
                    List<XafKeyframe> keys = clip.Channels[c].Keyframes;
                    for (int k = 0; k < keys.Count; k++)
                    {
                        float x = Mathf.Lerp(
                            bar.Position.X,
                            bar.End.X,
                            Mathf.Clamp(keys[k].Time / duration, 0f, 1f));
                        float distance = Mathf.Abs(input.Position.X - x);
                        if (distance >= best)
                            continue;
                        best = distance;
                        bestChannel = c;
                        bestKey = k;
                    }
                }

                if (bestChannel >= 0)
                {
                    _selectedKeyChannel = bestChannel;
                    _selectedKeyIndex = bestKey;
                    _runtime.Seek(clip.Channels[bestChannel]
                        .Keyframes[bestKey].Time);
                    _runtime.SetPaused(true);
                    SetStatus("KEY SELECTED • DELETE TO REMOVE");
                    _wasTimelinePressed = input.Pressed;
                    return;
                }
            }

            _selectedKeyChannel = -1;
            _selectedKeyIndex = -1;
            float t = Mathf.Clamp(
                (input.Position.X - bar.Position.X) / Mathf.Max(1f, bar.Size.X),
                0f,
                1f);
            _runtime.Seek(_runtime.CurrentDuration * t);
            _runtime.SetPaused(true);
        }
        _wasTimelinePressed = input.Pressed;
    }

    private void DeleteSelectedKey()
    {
        XafClip? clip = CurrentClip();
        if (clip is null ||
            _selectedKeyChannel < 0 ||
            _selectedKeyChannel >= clip.Channels.Count)
        {
            return;
        }

        XafChannel channel = clip.Channels[_selectedKeyChannel];
        if (_selectedKeyIndex < 0 ||
            _selectedKeyIndex >= channel.Keyframes.Count)
        {
            return;
        }

        float time = channel.Keyframes[_selectedKeyIndex].Time;
        XafAuthoring.RemoveKey(
            clip,
            channel.Target,
            channel.TargetId,
            channel.Property,
            time);
        _selectedKeyChannel = -1;
        _selectedKeyIndex = -1;
        SetStatus("KEY REMOVED");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Delete })
            DeleteSelectedKey();
    }

    private void DrawTimeline()
    {
        _canvas.Clear(_timelineCanvas);
        Rect2 bar = TimelineRect();
        _canvas.DrawTextStyled(
            _timelineCanvas,
            new Rect2(bar.Position.X, bar.Position.Y - 18,
                bar.Size.X, 16),
            $"{_runtime.CurrentClip}  •  {_runtime.CurrentTime:0.00}s / {_runtime.CurrentDuration:0.00}s",
            new Color("#94a3b8"),
            12);
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

        for (int c = 0; c < clip.Channels.Count; c++)
        {
            List<XafKeyframe> keys = clip.Channels[c].Keyframes;
            for (int k = 0; k < keys.Count; k++)
            {
                XafKeyframe key = keys[k];
                float keyX = Mathf.Lerp(
                    bar.Position.X,
                    bar.End.X,
                    Mathf.Clamp(key.Time / duration, 0f, 1f));
                bool selected = c == _selectedKeyChannel &&
                                k == _selectedKeyIndex;
                _canvas.DrawCircle(
                    _timelineCanvas,
                    new Vector2(keyX, bar.Position.Y + 19),
                    selected ? 6 : 4,
                    selected ? Colors.White : new Color("#38bdf8"));
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

    private void CloseFileMenu()
    {
        _fileMenuOpen = false;
        if (_fileMenu.HasComponent<Position>())
        {
            _fileMenu.GetComponent<Position>().Value =
                new Vector2(-10_000, -10_000);
        }
    }

    private void NudgeTransform(float rotation, float scale)
    {
        if (_selectedBone < 0)
            return;

        _runtime.SetPaused(true);
        XafTransform transform = _runtime.Pose.Bones[_selectedBone];
        transform.Rotation += rotation;
        transform.Scale += new Vector2(scale, scale);
        _runtime.Pose.Bones[_selectedBone] = transform;
        if (_autoKeyEnabled)
            KeySelectedBone();
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
            case ToolActionKind.ToggleFileMenu:
                _fileMenuOpen = !_fileMenuOpen;
                if (_fileMenu.HasComponent<Position>())
                {
                    _fileMenu.GetComponent<Position>().Value =
                        _fileMenuOpen
                            ? new Vector2(GetViewportRect().Size.X - 350, 120)
                            : new Vector2(-10_000, -10_000);
                }
                break;
            case ToolActionKind.Play:
                _playback.Play();
                _timeScale = 1f;
                SetStatus("PLAYING");
                break;
            case ToolActionKind.Stop:
                _playback.Stop();
                SetStatus("STOPPED");
                break;
            case ToolActionKind.AutoKey:
                _autoKeyEnabled = !_autoKeyEnabled;
                _autoKey.GetComponent<Text>().Value =
                    _autoKeyEnabled ? "AUTO KEY ON" : "AUTO KEY OFF";
                break;
            case ToolActionKind.Save:
                SavePackage();
                CloseFileMenu();
                break;
            case ToolActionKind.Load:
                _package = LoadPackage();
                _runtime = new AnimatorRuntime(_package);
                _playback.Replace(_runtime);
                _runtime.Play("idle", 0.15f);
                SetStatus("LOADED");
                CloseFileMenu();
                break;
            case ToolActionKind.Clip:
                _runtime.Play(action.Argument, 0.2f);
                _runtime.SetPaused(false);
                break;
            case ToolActionKind.RotateLeft:
                NudgeTransform(-0.25f, 1f);
                break;
            case ToolActionKind.RotateRight:
                NudgeTransform(0.25f, 1f);
                break;
            case ToolActionKind.ScaleDown:
                NudgeTransform(0f, -0.1f);
                break;
            case ToolActionKind.ScaleUp:
                NudgeTransform(0f, 0.1f);
                break;
            case ToolActionKind.KeyBone:
                KeySelectedBone();
                break;
            case ToolActionKind.Onion:
                _onionEnabled = !_onionEnabled;
                _onionButton.GetComponent<Text>().Value =
                    _onionEnabled ? "ONION ON" : "ONION OFF";
                SetStatus(_onionEnabled ? "ONION ON" : "ONION OFF");
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
        ToggleFileMenu,
        RotateLeft,
        RotateRight,
        ScaleUp,
        ScaleDown,
        KeyBone,
        Onion,
    }

    public struct ToolAction(ToolActionKind kind, string argument = "") : IComponent
    {
        public ToolActionKind Kind = kind;
        public string Argument = argument;
    }
}
