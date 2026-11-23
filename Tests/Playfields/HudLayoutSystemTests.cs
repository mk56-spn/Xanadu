using Xanadu.Storage.Settings;
using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Xanadu.Screens.Play;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class HudLayoutSystemTests : EcsSystemTest
{
    private RecordingCanvas _canvas = null!;
    private ScriptedPointer _pointer = null!;
    private FakeKeyboard _keyboard = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingCanvas();
        _pointer = new ScriptedPointer();
        _keyboard = new FakeKeyboard();
        Root.Add(new HudLayoutSystem(_canvas, _pointer, _keyboard, new HudLayoutSettings(persist: false)));
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public HashSet<Handle> Hidden { get; } = new();

        public void SetVisible(Handle canvas, bool visible)
        {
            if (!visible)
                Hidden.Add(canvas);
        }

        public void SetTransform(Handle canvas, Transform2D transform)
        {
        }

        public void Clear(Handle canvas)
        {
        }

        public void DrawRect(Handle canvas, Rect2 bounds, Color color)
        {
        }

        public void DrawLine(Handle canvas, Vector2 from, Vector2 to, Color color, float width)
        {
        }

        public void DrawText(Handle canvas, Rect2 bounds, string text, Color color)
        {
        }

        public void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color)
        {
        }

        public void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color)
        {
        }
    }
}
