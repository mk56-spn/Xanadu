using System;
using System.Globalization;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Drawers.Animator;
using Xanadu.Host;
using Xanadu.Logic.Animator;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Screens.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The rig (XIF) skeleton and animation tool on the MoonWorks host
/// (Docs/host.md §5.9): builds the animator's ECS world on
/// <see cref="HostApp"/>'s canvas and feeds it input.
/// </summary>
internal sealed class AnimatorTool : HostApp
{
    // The game's files, hooked in before anything loads particles or rig art.
    static AnimatorTool() => Xanadu.Storage.GameFiles.Install();

    private const string DefaultPath = AnimatorPaths.DefaultPackage;

    private readonly KeyPressQueue _keys = new();
    private readonly AnimatorArt _attachments;
    private readonly Entity _session;
    private bool _fitted;

    /// <summary>CLOSE EDITOR was confirmed (<see cref="CloseAsked"/>); quit on the next tick.</summary>
    private bool _closing;
    /// <summary>
    /// Holds the grid, the art and the bone overlay, clipped to the preview, so
    /// no part of the character ever draws over the panels or their buttons.
    /// It is never cleared, so its clip stays put while its children redraw.
    /// </summary>
    private readonly Handle _stage;
    private Rect2 _stageClip;
    private readonly Handle _grid;
    private readonly Handle _preview;
    private readonly Handle _artCanvas;
    private readonly Handle _timelineCanvas;

    /// <summary>The command-line set-up, applied on the second frame.</summary>
    private readonly AnimatorLaunch _launch;

    /// <summary>Where <see cref="AnimatorLaunch.ShotPath"/> still waits to be captured; cleared once taken.</summary>
    private string? _shotPath;
    private int _frames;
    private float _elapsed;

    private readonly MiniaudioAnimatorClick _click = new();

    /// <summary>The pointer the animator's systems read; <c>--perf-drag</c> takes it over.</summary>
    private readonly AnimatorScriptedPointer _pointer;

    private int _perfFrame = -1;
    private readonly System.Collections.Generic.List<float> _perfDeltas = [];
    private readonly System.Diagnostics.Stopwatch _perfClock = new();

    /// <summary>The animator's own systems; the host's UI steps them.</summary>
    private readonly SystemRoot _systems;

    public AnimatorTool(string[] args) : this(AnimatorLaunch.Read(args))
    {
    }

    private AnimatorTool(AnimatorLaunch launch)
        : base("Xanadu Animator", new HostWindowOptions(launch.Width, launch.Height, null, 0), HostUi.NoTooltips)
    {
        _launch = launch;
        _shotPath = launch.ShotPath;
        IRectCanvas canvas = UiCanvas;

        _stage = CreateCanvas(AnimatorLayout.GridZ);
        _grid = CreateCanvas(0, _stage);
        _artCanvas = CreateCanvas(AnimatorLayout.ArtZ - AnimatorLayout.GridZ, _stage);
        _preview = CreateCanvas(AnimatorLayout.PreviewZ - AnimatorLayout.GridZ, _stage);
        _timelineCanvas = CreateCanvas(AnimatorLayout.TimelineZ);
        // The part the XIF editor holds on its bone: over the editor's dimming, under its field lines.
        Handle heldCanvas = CreateCanvas(UiLayers.AnimatorXifItem);
        _attachments = new AnimatorArt(_artCanvas, heldCanvas);

        var screen = new Vector2(Width, Height);
        var toasts = new ToastSystem(Entities, screen, HostPlatform.Visuals);
        _systems = new SystemRoot(Entities, "Animator");
        _pointer = new AnimatorScriptedPointer(Pointer);
        _systems.Add(new AnimatorSystemGroup(
            canvas,
            _grid,
            _preview,
            _timelineCanvas,
            _attachments,
            HostPlatform.Visuals,
            _pointer,
            Pointer,
            _keys,
            _click,
            toasts,
            heldCanvas));
        _systems.Add(toasts);
        UseSystems(_systems);

        var files = new AnimatorFiles();
        _session = AnimatorSessionSetup.Create(
            Entities,
            AnimatorFileCommands.LoadOrDefault(files, DefaultPath),
            files,
            DefaultPath);
        _session.AddComponent(AnimatorLayout.Build(Entities, screen));
        _session.AddSignalHandler<CloseAsked>(_ => _closing = true);
    }

    /// <summary>Selects a cloth or a part by id (empty: the first part with art, else the first chain) and opens the XIF editor on it.</summary>
    private void EditXifOn(AnimatorLaunch.XifTarget target)
    {
        string id = target.Item;
        bool shader = target.Shader is not null || target.Layers.Length > 0;
        RigPackage package = _session.GetComponent<AnimatorDocument>().Package;
        int cloth = id.Length > 0 ? package.Cloths.FindIndex(c => c.Id == id) : -1;
        if (cloth >= 0)
        {
            ref AnimatorSelection picked = ref _session.GetComponent<AnimatorSelection>();
            picked.SelectOnlyCloth(cloth);
            picked.Chain = -1;
            if (shader)
                PointAt(package.Cloths[cloth].Node, target);
            AnimatorArtCommands.EditXif(_session);
            return;
        }
        RigView<RigAttachment> parts = package.Attachments;
        int index = id.Length > 0
            ? parts.FindIndex(part => part.Id == id)
            : parts.FindIndex(part => part.Node.Polygons.Count > 0);
        ref AnimatorSelection selection = ref _session.GetComponent<AnimatorSelection>();
        if (index >= 0)
        {
            selection.SelectOnlyAttachment(index);
            selection.Chain = -1;
        }
        else if (id.Length == 0 && _session.GetComponent<AnimatorDocument>().Package.Chains.Count > 0)
        {
            // No part has art yet: a chain's art is edited the same way.
            selection.Attachment = -1;
            selection.Chain = 0;
        }
        else
        {
            return;
        }
        if (shader && index >= 0)
            PointAt(parts[index].Node, target);
        AnimatorArtCommands.EditXif(_session);
    }

    /// <summary>Points the node at the target's common shader, if it names one, then stacks its layers over it.</summary>
    private static void PointAt(Xif node, AnimatorLaunch.XifTarget target)
    {
        if (target.Shader is not null)
            XifEditing.SetShader(node, XifShaderRef.OfCommon(target.Shader));
        foreach (string layer in target.Layers)
            XifEditing.AddLayer(node, layer);
    }

    protected override void BeforeSystems(float delta)
    {
        // Over the XIF editor only Esc (close it) and undo and redo reach the animator's shortcuts.
        bool xif = XifEditorSystem.IsOpen(_session.GetComponent<AnimatorUi>().XifEditor);
        Keyboard.FeedPresses(Inputs.Keyboard, _keys, key =>
            !TerminalOpen && key != DebugKeys.Screenshot && (!TextInput.Active || key == Key.Escape) &&
            (!xif || key is Key.Escape or Key.Z or Key.Y));
    }

    protected override void Tick(float delta, float time)
    {

        if (_closing)
        {
            Quit();
            return;
        }

        ClipStage();

        // Fit once the panels have been laid out, so the rig opens filling the preview.
        if (!_fitted && AnimatorPreviewCommands.Fit(_session))
        {
            _fitted = true;
            if (_launch.Zoom is > 0f and var zoom)
                _session.GetComponent<AnimatorPreviewView>().Zoom = zoom;
            if (_launch.TimelineZoom is > 0f and var timelineZoom)
                _session.GetComponent<TimelineView>().Zoom = timelineZoom;
        }

        _frames++;
        if (_frames > 2)
            _elapsed += delta;
        if (_frames == 2)
            ApplyLaunch();
        if (_frames == 3 && _launch.KeyNow)
            _session.EmitSignal(new KeyBone());
        if (_launch.Perf is { } perf)
            PerfDrag(perf);
        bool due = _launch.ShotSeconds is { } seconds ? _elapsed >= seconds : _frames == _launch.ShotFrames;
        if (_shotPath is not null && due)
        {
            RequestShot(_shotPath);
            _shotPath = null;
        }
    }

    /// <summary>The command-line set-up, once the layout exists; rig names are looked up here.</summary>
    private void ApplyLaunch()
    {
        if (_launch.Flow is { } flow)
            StartFlow(flow);
        if (_launch.Clip is { } clip)
            OpenClipAt(clip);
        if (_launch.Easing)
            _session.EmitSignal(new ToggleEasingWindow());
        if (_launch.Attacks)
            _session.EmitSignal(new CycleAttacks());
        if (_launch.Streak is { } streak)
            AddOnBone(streak, new AddEffect(), 0.05f);
        if (_launch.Sparks is { } sparks)
            AddOnBone(sparks, new AddParticles(), 0.2f);
        if (_launch.Dress is { } dress)
        {
            SelectBone(dress);
            _session.EmitSignal(new AddCloth());
        }
        // After --dress, so "--edit-xif=dress" finds the cloth it adds.
        if (_launch.EditXif is { } xif)
            EditXifOn(xif);
        if (_launch.Part is { } part)
            _session.EmitSignal(new SelectPart(part));
        if (_launch.Paint is { } paint)
        {
            _session.EmitSignal(new PaintWeights());
            int bone = Bone(paint);
            if (bone >= 0)
                AnimatorWeightCommands.PickBone(_session, bone);
        }
        if (_launch.Cloth is { } cloth)
            _session.EmitSignal(new SelectCloth(cloth));
        if (_launch.Paths is { } paths)
            ShowPaths(paths);
        if (_launch.AddCurve is { } curvePart)
            AddCurveTo(curvePart);
        if (_launch.Shortcuts)
            _session.EmitSignal(new ToggleShortcuts());
        if (_launch.EditRig)
            _session.EmitSignal(new ToggleMode(AnimatorMode.Rest));
        if (_launch.Layers)
            Entities.Query<UiTabs>().ToEntityList()[0].GetComponent<UiTabs>().Selected = AnimatorLayout.LayersTab;
        if (_launch.FullKeys)
            _session.SetTag<FullKeys>(true);
        foreach (string bone in _launch.Plant)
        {
            SelectBone(bone);
            _session.EmitSignal(new PlantBone());
        }
        foreach (string bone in _launch.Orbit)
        {
            SelectBone(bone);
            _session.EmitSignal(new AddOrbit());
        }
        if (_launch.Preview)
            _session.EmitSignal(new TogglePreviewMode());
    }

    /// <summary>A bone's index in the loaded rig, by the name it was given on the command line; -1 if there is none.</summary>
    private int Bone(string name) => RigRig.FindBone(_session.GetComponent<AnimatorDocument>().Package, name);

    private void SelectBone(string name) =>
        _session.EmitSignal(new SelectBone(Bone(name)));

    private const int PerfStart = 30;
    private const int PerfFrames = 300;
    private static readonly float PerfRadius = float.TryParse(Environment.GetEnvironmentVariable("PERF_RADIUS"), CultureInfo.InvariantCulture, out float r) ? r : 80f;

    /// <summary>
    /// <c>--perf-drag=bone</c>: from frame 30, holds the joint and drags it
    /// round a circle for 300 frames, then prints the frame times and each
    /// animator system's share. Pair it with <c>--shot-frames=400</c> to quit.
    /// </summary>
    private void PerfDrag(AnimatorLaunch.PerfDrag perf)
    {
        if (_frames < PerfStart || _perfFrame >= PerfFrames)
            return;

        RigPackage package = _session.GetComponent<AnimatorDocument>().Package;
        int bone = RigRig.FindBone(package, perf.Bone);
        Rect2 area = AnimatorGeometry.PanelArea(_session.GetComponent<AnimatorUi>().Preview);
        if (bone < 0)
        {
            Console.WriteLine($"perf-drag: no bone '{perf.Bone}'");
            _perfFrame = PerfFrames;
            return;
        }

        RigPose pose = AnimatorGeometry.PreviewPose(_session.GetComponent<AnimatorDocument>(), AnimatorModes.Of(_session));
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, _session.GetComponent<AnimatorPreviewView>());
        if (_perfFrame < 0)
        {
            if (perf.Ik)
                _session.EmitSignal(new ToggleMode(AnimatorMode.Ik));
            Vector2 joint = placement * RigRig.WorldBones(package, pose)[bone].Origin;
            _pointer.Override = new PointerInput(joint, true);
            var begin = System.Diagnostics.Stopwatch.StartNew();
            AnimatorPreviewGrab.Begin(_session, joint, area, false);
            Console.WriteLine($"perf-drag: Begin took {begin.Elapsed.TotalMilliseconds:0.0} ms");
            AppStep.MonitorAll(_systems);
            _perfClock.Restart();
            _perfFrame = 0;
            Console.WriteLine($"perf-drag: holding {perf.Bone}, drag kind {_session.GetComponent<AnimatorDrag>().Kind}");
            return;
        }

        float ms = (float)_perfClock.Elapsed.TotalMilliseconds;
        _perfDeltas.Add(ms);
        if (ms > 30f)
            Console.WriteLine($"perf-drag: frame {_perfFrame} took {ms:0.0} ms (gen0 {GC.CollectionCount(0)}, gen1 {GC.CollectionCount(1)}, gen2 {GC.CollectionCount(2)})");
        _perfClock.Restart();
        _perfFrame++;
        Vector2 start = _pointer.Override!.Value.Position;
        float angle = _perfFrame * 0.05f;
        _pointer.Override = new PointerInput(start + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * PerfRadius, true);

        if (_perfFrame < PerfFrames)
            return;

        _pointer.Override = null;
        _perfDeltas.Sort();
        float mean = 0f;
        foreach (float d in _perfDeltas)
            mean += d;
        mean /= _perfDeltas.Count;
        Console.WriteLine($"perf-drag: {_perfDeltas.Count} frames, mean {mean:0.00} ms, median {_perfDeltas[_perfDeltas.Count / 2]:0.00} ms, worst {_perfDeltas[^1]:0.00} ms");
        Console.WriteLine(_systems.GetPerfLog());
    }

    /// <summary>
    /// For <c>--add-curve=N</c>: gives part N a curve shape over its art, two
    /// sides bulging out and two bending in, then frames it, to look at the GPU curve fill.
    /// </summary>
    private void AddCurveTo(int part)
    {
        RigPackage package = _session.GetComponent<AnimatorDocument>().Package;
        if (part >= package.Attachments.Count)
            return;
        AnimatorHistoryCommands.Edit(_session, () =>
        {
            Xif node = package.Attachments[part].Node;
            XifGeometry? old = node.Get<XifGeometry>();
            var geometry = (XifGeometry?)old?.Copy() ?? new XifGeometry();
            geometry.Polygons.Add(new XifPolygon
            {
                Kind = XifShapeKind.Curve,
                Fill = new Color(0.95f, 0.55f, 0.2f),
                Points =
                [
                    new Vector2(40, -40), new Vector2(75, 0),
                    new Vector2(40, 40), new Vector2(0, 12),
                    new Vector2(-40, 40), new Vector2(-75, 0),
                    new Vector2(-40, -40), new Vector2(0, -12),
                ],
            });
            if (old is not null)
                node.Modules.Remove(old);
            node.Modules.Add(geometry);
            AnimatorSession.RigChanged(_session);
        });
        _session.EmitSignal(new SelectPart(part));
        _session.EmitSignal(new FitPreview());
    }

    private void StartFlow(AnimatorLaunch.Handover flow)
    {
        foreach (Entity strip in Entities.Query<UiTabs>().Entities)
            strip.GetComponent<UiTabs>().Selected = 2;
        _session.EmitSignal(new PlayClip(flow.From));
        _session.EmitSignal(new SelectTransition(flow.To));
        _session.EmitSignal(new PreviewTransition());
    }

    private void OpenClipAt(AnimatorLaunch.ClipAt clip)
    {
        _session.EmitSignal(new PlayClip(clip.Clip));
        AnimatorRuntime runtime = _session.GetComponent<AnimatorDocument>().Runtime;
        runtime.SetPaused(true);
        if (clip.Seconds is { } seconds)
            runtime.Seek(seconds);
    }

    private void ShowPaths(AnimatorLaunch.MotionPaths paths)
    {
        _session.SetTag<ShowMotionPaths>(true);
        if (paths.Clip is { } clip)
        {
            _session.EmitSignal(new PlayClip(clip));
            AnimatorRuntime runtime = _session.GetComponent<AnimatorDocument>().Runtime;
            runtime.SetPaused(true);
            runtime.Seek(runtime.CurrentDuration / 2);
        }

        for (int i = 0; i < paths.Bones.Length; i++)
        {
            int bone = Bone(paths.Bones[i]);
            if (bone < 0)
                continue;
            ref AnimatorSelection selection = ref _session.GetComponent<AnimatorSelection>();
            if (i == 0)
                selection.SelectOnlyBone(bone);
            else
                selection.ToggleBone(bone);
        }
    }

    /// <summary>Hangs a streak or particle stream off a bone from the start, paused a little in, for a screenshot of it.</summary>
    private void AddOnBone(string bone, IIntent add, float showAt)
    {
        SelectBone(bone);
        AnimatorRuntime runtime = _session.GetComponent<AnimatorDocument>().Runtime;
        runtime.SetPaused(true);
        runtime.Seek(0f);
        add.EmitOn(_session);
        _session.GetComponent<AnimatorDocument>().Runtime.Seek(showAt);
    }

    protected override void Shutdown()
    {
        _attachments.Release();
        foreach (Handle canvas in new[] { _grid, _preview, _artCanvas, _timelineCanvas, _stage })
            CanvasServer.Current.Free(canvas);
        _click.Dispose();
    }

    /// <summary>Keeps the stage's clip on the preview as the layout moves it (preview mode, resizes).</summary>
    private void ClipStage()
    {
        Rect2 area = AnimatorGeometry.PanelArea(_session.GetComponent<AnimatorUi>().Preview);
        if (area == _stageClip)
            return;

        _stageClip = area;
        CanvasServer.Current.SetCustomRect(_stage, true, area);
        CanvasServer.Current.SetClip(_stage, true);
    }

    /// <summary>A canvas under <paramref name="parent"/> (the tool's own item by default); z is relative to it.</summary>
    private Handle CreateCanvas(int zIndex, Handle parent = default)
    {
        Handle canvas = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(canvas, parent.IsValid ? parent : Canvas.RootHandle);
        CanvasServer.Current.SetZIndex(canvas, zIndex);
        return canvas;
    }
}
