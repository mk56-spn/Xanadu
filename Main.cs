using System;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Misc;
using Xanadu.Editor;
using Xanadu.Strings;
using Xanadu.Systems.Input;

namespace Xanadu;

// Godot lifecycle only. System wiring lives in XanaduSystemRoot.
public partial class Main : Control
{
	public EntityStore Entities { get; } = new();

	private XanaduSystemRoot? _systems;
	private GodotPointerInputSource? _pointer;
	private SceneDirector? _sceneDirector;
	private EditorOverlay? _editorOverlay;
	private OffsetWizardOverlay? _offsetWizard;
	private SongSelectOverlay? _songSelectOverlay;
	private SongTimingOverlay? _songTimingOverlay;

	public override void _Ready()
	{
		LocalizedText.EnableGodot();
		SongStore.CatalogFilePath =
			ProjectSettings.GlobalizePath("res://Songs/catalog.json");
		SetProcess(true);
		_pointer = new GodotPointerInputSource(this);
		_sceneDirector = new SceneDirector();
		_sceneDirector.Changed += OnSceneChanged;
		_systems = new XanaduSystemRoot(
			Entities,
			GetCanvasItem(),
			_pointer,
			_sceneDirector,
			GetViewportRect().Size);

		_systems.Scenes.Start(SceneKind.Profiles);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false } key &&
			(key.Keycode == Key.F12 || key.PhysicalKeycode == Key.F12))
		{
			CaptureScreenshot();
			GetViewport().SetInputAsHandled();
		}
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

		if (@event is InputEventMouseButton
			{
				Pressed: true,
				ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown,
			} wheel && _editorOverlay is null)
		{
			_systems?.Scroll(
				GetViewport().GetMousePosition(),
				wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
			GetViewport().SetInputAsHandled();
			return;
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

			    case Key.Bracketleft:
				    if (_systems?.Scenes.CyclePreviewStart(-1) == true)
					    GetViewport().SetInputAsHandled();
				    break;

			    case Key.Bracketright:
				    if (_systems?.Scenes.CyclePreviewStart(1) == true)
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

	private void CaptureScreenshot()
	{
		try
		{
			Image? image = GetViewport().GetTexture().GetImage();
			if (image is null)
			{
				_systems?.Toasts.Show("Screenshot failed: no viewport image");
				return;
			}

			string gameName = ProjectSettings
				.GetSetting("application/config/name")
				.AsString();
			string[] candidates =
			{
				ProjectSettings.GlobalizePath("user://screenshots"),
				ProjectSettings.GlobalizePath("res://screenshots"),
			};

			foreach (string directory in candidates)
			{
				try
				{
					Directory.CreateDirectory(directory);
					string file = ScreenshotNaming.Create(
						gameName,
						DateTime.Now,
						candidate => File.Exists(Path.Combine(directory, candidate)));
					string path = Path.Combine(directory, file);
					if (image.SavePng(path) == Error.Ok)
					{
						_systems?.Toasts.Show($"Screenshot saved: {file}");
						return;
					}
				}
				catch (Exception)
				{
					// Try the next writable location.
				}
			}

			_systems?.Toasts.Show("Screenshot failed: no writable directory");
		}
		catch (Exception exception)
		{
			_systems?.Toasts.Show($"Screenshot failed: {exception.Message}");
		}
	}

	private void OnSceneChanged(SceneKind scene)
	{
		_systems?.Commands.Clear();

		if (scene == SceneKind.Editor)
		{
			_systems?.EditorHistory.Clear();
			_offsetWizard?.Close();
			_offsetWizard = null;

			if (_editorOverlay is not null)
				return;

			_editorOverlay = new EditorOverlay(
				Entities,
				_systems!.Commands,
				_pointer!,
				() => _systems?.Scenes.TryBack(),
				() => _systems?.Scenes.TryForward(),
				(document, path, playheadMs) => _systems?.Scenes.StartEditorPreview(
					new LevelEntry(
						path,
						document,
						new SongStore().Find(document.SongId)),
					playheadMs));
			AddChild(_editorOverlay);
			_systems.Scenes.SaveEditor = () => _editorOverlay?.SaveCurrent() ?? false;
			return;
		}

		if (_editorOverlay is not null)
		{
			_editorOverlay.Close();
			_editorOverlay = null;
		}

		if (scene == SceneKind.SongTiming)
		{
			_songSelectOverlay?.QueueFree();
			_songSelectOverlay = null;
			if (_songTimingOverlay is not null)
				return;

			_songTimingOverlay = new SongTimingOverlay(_systems!.Scenes);
			AddChild(_songTimingOverlay);
			return;
		}

		_songTimingOverlay?.QueueFree();
		_songTimingOverlay = null;

		if (scene == SceneKind.SongSelect)
		{
			if (_songSelectOverlay is not null)
				return;

			_songSelectOverlay = new SongSelectOverlay(
				() => _systems?.Scenes.SelectedLevel,
				() => _systems?.Scenes.ReloadCurrent(),
				entry => _systems?.Scenes.OpenSongTiming(
					entry.Document.SongId,
					SceneKind.SongSelect));
			AddChild(_songSelectOverlay);
			return;
		}

		_songSelectOverlay?.QueueFree();
		_songSelectOverlay = null;

		if (scene == SceneKind.OffsetWizard)
		{
			if (_offsetWizard is not null)
				return;

			_offsetWizard = new OffsetWizardOverlay(
				_systems!.Offsets,
				() => _systems?.Scenes.TryBack());
			AddChild(_offsetWizard);
			return;
		}

		_offsetWizard?.Close();
		_offsetWizard = null;
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
		_offsetWizard?.Close();
		_offsetWizard = null;
		_systems?.Dispose();
		_systems = null;
	}
}
