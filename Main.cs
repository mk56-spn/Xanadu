using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Scenes;
using Writer.Systems.Input;

namespace Writer;

// Godot lifecycle only. System wiring lives in WriterSystemRoot.
public partial class Main : Control
{
	public EntityStore Entities { get; } = new();

	[Export] public bool StressTest { get; set; }

	[Export] public bool ContainerTest { get; set; }

	[Export] public bool TrackTest { get; set; }

	[Export] public bool PhysicsTest { get; set; }

	[Export] public bool EditorTest { get; set; }

	private WriterSystemRoot? _systems;
	private GodotPointerInputSource? _pointer;
	private FileDialog? _trackDialog;

	public override void _Ready()
	{
		SetProcess(true);
		_pointer = new GodotPointerInputSource(this);
		_systems = new WriterSystemRoot(Entities, GetCanvasItem(), _pointer);

		if (StressTest)
			StressScene.Build(Entities, GetViewportRect().Size);
		else if (ContainerTest)
			ContainerScene.Build(Entities, GetViewportRect().Size);
		else if (TrackTest)
			TrackScene.Build(Entities, GetViewportRect().Size);
		else if (PhysicsTest)
			PhysicsScene.Build(Entities, _systems.Physics, GetViewportRect().Size);
		else if (EditorTest)
		{
			EditorScene.Build(Entities, GetViewportRect().Size);
			AddChild(new EditorOverlay(
				Entities,
				_systems.Commands,
				_pointer));
		}
		else
			TestScene.Build(Entities);

		if (!TrackTest)
			return;

		SetupTrackLoader();

		string[] args = OS.GetCmdlineUserArgs();
		if (args.Length > 0 && File.Exists(args[0]))
			TrackScene.Load(Entities, Path.GetFullPath(args[0]));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton
			{
				ButtonIndex: MouseButton.Left,
				Pressed: true,
			})
		{
			_pointer?.LatchPress();
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_trackDialog is null || _trackDialog.Visible)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.L })
			OpenTrackDialog();
	}

	private void SetupTrackLoader()
	{
		_trackDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Filesystem,
			UseNativeDialog = false,
			Title = "Load a song",
			Filters = ["*.mp3, *.wav, *.ogg, *.flac ; Audio Files"],
		};

		_trackDialog.FileSelected += OnTrackFileSelected;
		AddChild(_trackDialog);
	}

	private void OpenTrackDialog()
	{
		if (_trackDialog is null || _trackDialog.Visible)
			return;

		_trackDialog.PopupCentered(new Vector2I(900, 600));
	}

	private void OnTrackFileSelected(string path) =>
		TrackScene.Load(Entities, path);

	public override void _Process(double delta)
	{
		float time = (float)Time.GetTicksMsec() / 1000f;
		_systems?.Update(new UpdateTick((float)delta, time));
	}

	public override void _Draw() => DrawCircle(Vector2.Zero, 100, Colors.Red);

	public override void _ExitTree()
	{
		_systems?.Dispose();
		_systems = null;
	}
}
