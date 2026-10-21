using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Input;

namespace Writer;

// Godot lifecycle only. System wiring lives in WriterSystemRoot.
public partial class Main : Control
{
	public EntityStore Entities { get; } = new();

	private WriterSystemRoot? _systems;
	private GodotPointerInputSource? _pointer;
	private SceneDirector? _sceneDirector;
	private EditorOverlay? _editorOverlay;

	public override void _Ready()
	{
		SetProcess(true);
		_pointer = new GodotPointerInputSource(this);
		_sceneDirector = new SceneDirector();
		_sceneDirector.Changed += OnSceneChanged;
		_systems = new WriterSystemRoot(
			Entities,
			GetCanvasItem(),
			_pointer,
			_sceneDirector,
			GetViewportRect().Size);

		_systems.Scenes.Start(SceneKind.Main);
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

		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;

		Key keycode = key.Keycode != Key.None
			? key.Keycode
			: key.PhysicalKeycode;

		if (_systems?.Keybinds.TryCapture(keycode) == true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_editorOverlay is not null)
			return;

		switch (keycode)
		{
			case Key.Left:
				if (_systems?.Scenes.TryStepCarousel(-1) == true)
					GetViewport().SetInputAsHandled();
				break;

			case Key.Right:
				if (_systems?.Scenes.TryStepCarousel(1) == true)
					GetViewport().SetInputAsHandled();
				break;

			case Key.Escape:
				_systems?.Scenes.TryBack();
				GetViewport().SetInputAsHandled();
				break;

			case Key.Enter:
			case Key.KpEnter:
				_systems?.Scenes.TryForward();
				GetViewport().SetInputAsHandled();
				break;
		}
	}

	private void OnSceneChanged(SceneKind scene)
	{
		_systems?.Commands.Clear();

		if (scene == SceneKind.Editor)
		{
			if (_editorOverlay is not null)
				return;

			_editorOverlay = new EditorOverlay(
				Entities,
				_systems!.Commands,
				_pointer!,
				() => _systems?.Scenes.TryBack(),
				() => _systems?.Scenes.TryForward());
			AddChild(_editorOverlay);
			return;
		}

		if (_editorOverlay is null)
			return;

		_editorOverlay.Close();
		_editorOverlay = null;
	}

	public override void _Process(double delta)
	{
		float time = (float)Time.GetTicksMsec() / 1000f;
		_systems?.Update(new UpdateTick((float)delta, time));
	}

	public override void _ExitTree()
	{
		if (_sceneDirector is not null)
			_sceneDirector.Changed -= OnSceneChanged;

		_editorOverlay?.Close();
		_editorOverlay = null;
		_systems?.Dispose();
		_systems = null;
	}
}
