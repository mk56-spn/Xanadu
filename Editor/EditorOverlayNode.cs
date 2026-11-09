using Godot;

namespace Xanadu.Editor;

/// <summary>
/// Runs an <see cref="EditorOverlay"/> under the Godot build: ticks it each
/// frame and gives it Godot's file dialog for shader imports. The MoonWorks host
/// ticks the overlay itself (docs/moonworks-port.md step 9). Goes with Godot in step 11.
/// </summary>
public sealed partial class EditorOverlayNode : Node
{
    private readonly FileDialog _shaderFileDialog;
    private System.Action<string>? _picked;

    public EditorOverlay Overlay { get; }

    public EditorOverlayNode(EditorOverlay overlay)
    {
        Overlay = overlay;
        _shaderFileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = false,
            Title = "Import shader",
            Filters = ["*.slang ; Slang shaders"],
        };
        _shaderFileDialog.FileSelected += path => _picked?.Invoke(path);
        AddChild(_shaderFileDialog);

        overlay.PickShaderFile = then =>
        {
            _picked = then;
            _shaderFileDialog.PopupCentered(new Vector2I(900, 600));
        };
        overlay.FileDialogOpen = () => _shaderFileDialog.Visible;
    }

    public override void _Process(double delta) => Overlay.Tick();

    public void Close()
    {
        Overlay.Close();
        QueueFree();
    }
}
