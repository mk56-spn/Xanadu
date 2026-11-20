using System;
using System.Runtime.CompilerServices;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A tab with a circle in it. It is not registered in <see cref="LabExperiments.All"/>.
/// Before Main runs, a module initializer takes the runtime's entry stub for
/// that method (an x64 FixupPrecode: <c>jmp [rip+disp32]</c>), decodes the
/// displacement by hand to find the stub's target cell, and writes the
/// address of <see cref="Hijacked"/> into it. Everyone who calls the list,
/// through the stub or straight through the cell, now lands here. The
/// hijacker puts the old target back, calls the real list (which compiles
/// it and moves the cell on), re-arms the cell, and adds this tab at the end.
/// Its own name is an interned string literal kept backwards and reversed in
/// place. For shits and giggles only: do not learn from this.
/// </summary>
internal sealed unsafe class VoodooCircle : ILabExperiment
{
    private static nint _victim;
    private static nint* _cell;
    private static nint _trap;

    public string Name => "ELCRIC";

    public string[] Notes =>
    [
        "This tab was never registered.",
        "At startup the precode stub of",
        "LabExperiments.All() had its jump",
        "cell hand-decoded and pointed here.",
        "Its name was an intern ed literal,",
        "reversed in place. It is a circle.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    [ModuleInitializer]
    internal static void Summon()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            != System.Runtime.InteropServices.Architecture.X64)
            return;

        // The literal written here would be a private copy (run-once code gets
        // its own); the getter hands out the real interned one, so ask it.
        string name = new VoodooCircle().Name;
        System.Runtime.InteropServices.MemoryMarshal.CreateSpan(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(name.AsSpan()), name.Length).Reverse();

        delegate*<ILabExperiment[]> victim = &LabExperiments.All;
        delegate*<ILabExperiment[]> hijacker = &Hijacked;
        _victim = (nint)victim;
        _trap = (nint)hijacker;

        byte* stub = (byte*)_victim;
        if (stub[0] != 0xFF || stub[1] != 0x25)
            return; // not a FixupPrecode: the spirits are elsewhere today

        _cell = (nint*)(stub + 6 + Unsafe.ReadUnaligned<int>(stub + 2));
        *_cell = _trap;
    }

    private static ILabExperiment[] Hijacked()
    {
        *_cell = _victim + 6;
        ILabExperiment[] real;
        try
        {
            real = ((delegate*<ILabExperiment[]>)_victim)();
        }
        finally
        {
            *_cell = _trap;
        }

        return [.. real, new VoodooCircle()];
    }

    private sealed class Stage : IToolStage
    {
        private readonly Handle _item;

        public Stage(Handle parent, Rect2 area)
        {
            _item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(_item, parent);
            CanvasServer.Current.AddRect(_item, area, Palette.Stage);
            Vector2 middle = area.Position + area.Size * 0.5f;
            float radius = MathF.Min(area.Size.X, area.Size.Y) * 0.3f;
            CanvasServer.Current.AddCircle(_item, middle, radius, Palette.PrimarySoft);
        }

        public void Update(in ToolFrame frame)
        {
        }

        public void Dispose() => CanvasServer.Current.Free(_item);
    }
}
