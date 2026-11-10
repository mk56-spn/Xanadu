using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using BlendFactor = Xanadu.Rendering.BlendFactor;
using BlendOp = Xanadu.Rendering.BlendOp;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Two overlapping quads over a backdrop, each with the whole fixed-function
/// blend open: ops, factors for colour and alpha apart, the write mask and the
/// blend constant. Godot's canvas only ever offered five of these.
/// </summary>
internal sealed class BlendLab : ILabExperiment
{
    private const float Inner = LabPanel.Inner;
    private const float RowHeight = LabPanel.RowHeight;

    private static readonly BlendFactor[] Factors = Enum.GetValues<BlendFactor>();
    private static readonly BlendOp[] Ops = Enum.GetValues<BlendOp>();
    private static readonly string[] Backdrops = ["BANDS", "CHECKER", "BLACK", "WHITE", "GREY"];

    private static readonly (string Name, CustomBlend Blend)[] Presets =
    [
        ("MIX", Preset(BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha)),
        ("ADD", Preset(BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.One)),
        ("SUB", Preset(BlendOp.ReverseSubtract, BlendFactor.SrcAlpha, BlendFactor.One)),
        ("MUL", Preset(BlendOp.Add, BlendFactor.DstColor, BlendFactor.Zero)),
        ("SCREEN", Preset(BlendOp.Add, BlendFactor.One, BlendFactor.OneMinusSrcColor)),
        ("PREMUL", Preset(BlendOp.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)),
        ("MIN", Preset(BlendOp.Min, BlendFactor.One, BlendFactor.One)),
        ("MAX", Preset(BlendOp.Max, BlendFactor.One, BlendFactor.One)),
        ("INVERT", Preset(BlendOp.Add, BlendFactor.OneMinusDstColor, BlendFactor.Zero)),
        ("ERASE", Preset(BlendOp.Add, BlendFactor.Zero, BlendFactor.One) with
            { SrcAlpha = BlendFactor.Zero, DstAlpha = BlendFactor.OneMinusSrcAlpha }),
        ("PUNCH", Preset(BlendOp.Add, BlendFactor.Zero, BlendFactor.OneMinusSrcAlpha) with
            { SrcAlpha = BlendFactor.Zero, DstAlpha = BlendFactor.OneMinusSrcAlpha }),
        ("CLEAR", Preset(BlendOp.Add, BlendFactor.Zero, BlendFactor.Zero) with
            { SrcAlpha = BlendFactor.Zero, DstAlpha = BlendFactor.Zero }),
        ("CONST", Preset(BlendOp.Add, BlendFactor.ConstantColor, BlendFactor.OneMinusConstantColor)),
        ("REPLACE", CustomBlend.Mix with { Enabled = false }),
    ];

    private readonly Quad[] _quads =
    [
        new() { Colour = new Color(1f, 0.5f, 0.1f, 0.8f) },
        new() { Colour = new Color(0.1f, 0.6f, 1f, 0.8f) },
    ];

    private readonly LabPanel _ui = new();
    private int _selected = 1;
    private int _backdrop;
    private Color _constant = new(1f, 0.25f, 0.75f, 0.5f);
    private float _backdropAlpha = 1f;
    private bool _showAlpha;

    private readonly Entity[] _pick = new Entity[2];
    private readonly Entity[] _presets = new Entity[Presets.Length];
    private Entity _enabled, _ramp, _alphaView;
    private readonly Entity[] _write = new Entity[4];
    private readonly Entity[] _colour = new Entity[4];
    private readonly Entity[] _constants = new Entity[4];
    private Entity _backdropAlphaSlider;
    private LabStepper _backdropStepper = null!;
    private Entity _colourEquation, _alphaEquation;

    public string Name => "BLEND";

    public string[] Notes =>
    [
        "src × SRC FACTOR (op) dst × DST FACTOR, colour and alpha apart.",
        "QUAD A draws first, then B. The CONST rows and backdrop are shared.",
    ];

    private static CustomBlend Preset(BlendOp op, BlendFactor src, BlendFactor dst) =>
        CustomBlend.Mix with { ColorOp = op, SrcColor = src, DstColor = dst };

    private Quad Selected => _quads[_selected];

    // ---- panel --------------------------------------------------------------

    public void BuildPanel(UiNode panel)
    {
        UiNode pick = LabPanel.Row(panel);
        _pick[0] = pick.Button("QUAD A", UiType.Label).Size(Inner / 2 - 4, RowHeight);
        _pick[1] = pick.Button("QUAD B", UiType.Label).Size(Inner / 2 - 4, RowHeight);

        const int perRow = 7;
        float presetWidth = (Inner - 4 * (perRow - 1)) / perRow;
        UiNode presets = default;
        for (int i = 0; i < Presets.Length; i++)
        {
            if (i % perRow == 0)
                presets = LabPanel.Row(panel);
            _presets[i] = presets.Button(Presets[i].Name, UiType.Caption).Size(presetWidth, RowHeight)
                .Tooltip("Set the selected quad's blend to " + Presets[i].Name);
        }

        _ui.Stepper(panel, "COLOUR OP", () => Array.IndexOf(Ops, Selected.Blend.ColorOp),
            i => Selected.Blend = Selected.Blend with { ColorOp = Ops[i] }, Ops.Length, i => OpName(Ops[i]));
        _ui.Stepper(panel, "SRC COLOUR", () => Array.IndexOf(Factors, Selected.Blend.SrcColor),
            i => Selected.Blend = Selected.Blend with { SrcColor = Factors[i] }, Factors.Length, i => FactorName(Factors[i]));
        _ui.Stepper(panel, "DST COLOUR", () => Array.IndexOf(Factors, Selected.Blend.DstColor),
            i => Selected.Blend = Selected.Blend with { DstColor = Factors[i] }, Factors.Length, i => FactorName(Factors[i]));
        _ui.Stepper(panel, "ALPHA OP", () => Array.IndexOf(Ops, Selected.Blend.AlphaOp),
            i => Selected.Blend = Selected.Blend with { AlphaOp = Ops[i] }, Ops.Length, i => OpName(Ops[i]));
        _ui.Stepper(panel, "SRC ALPHA", () => Array.IndexOf(Factors, Selected.Blend.SrcAlpha),
            i => Selected.Blend = Selected.Blend with { SrcAlpha = Factors[i] }, Factors.Length, i => FactorName(Factors[i]));
        _ui.Stepper(panel, "DST ALPHA", () => Array.IndexOf(Factors, Selected.Blend.DstAlpha),
            i => Selected.Blend = Selected.Blend with { DstAlpha = Factors[i] }, Factors.Length, i => FactorName(Factors[i]));

        UiNode flags = LabPanel.Row(panel);
        _enabled = flags.Button("", UiType.Caption).Size(140, RowHeight)
            .With(new Toggle("BLEND ON", "BLEND OFF", true))
            .Tooltip("Off: the quad replaces what is under it, factors ignored");
        flags.Text("WRITE", UiType.Caption, Palette.TextDim).Size(64, RowHeight);
        string[] channels = ["R", "G", "B", "A"];
        for (int i = 0; i < 4; i++)
            _write[i] = flags.Button("", UiType.Caption).Size(48, RowHeight)
                .With(new Toggle(channels[i], channels[i], true))
                .Tooltip("Whether the quad writes the " + channels[i] + " channel at all");
        _ramp = flags.Button("", UiType.Caption).Size(120, RowHeight)
            .With(new Toggle("RAMP", "RAMP", false))
            .Tooltip("Fade the quad's alpha from 0 on its left edge to its colour's alpha on its right");

        string[] rgba = ["RED", "GREEN", "BLUE", "ALPHA"];
        for (int i = 0; i < 4; i++)
            _colour[i] = LabPanel.Slider(panel, rgba[i], 0, 1, 0, "The selected quad's colour");

        _colourEquation = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);
        _alphaEquation = panel.Text("", UiType.Small, Palette.Accent).Mono().Size(Inner, RowHeight);

        string[] constant = ["CONST R", "CONST G", "CONST B", "CONST A"];
        for (int i = 0; i < 4; i++)
            _constants[i] = LabPanel.Slider(panel, constant[i], 0, 1, _constant[i], "The blend constant the CONST factors read");

        _backdropStepper = _ui.Stepper(panel, "BACKDROP", () => _backdrop, i => _backdrop = i, Backdrops.Length,
            i => Backdrops[i], spare: 148);
        _backdropAlphaSlider = LabPanel.Slider(panel, "BACK ALPHA", 0, 1, 1, "The backdrop's alpha, which the DST ALPHA factors read");
        _alphaView = _backdropStepper.Row.Button("", UiType.Caption).Size(140, RowHeight)
            .With(new Toggle("SHOW ALPHA", "SHOW ALPHA", false))
            .Tooltip("Paint the target's alpha as grey (itself a blend: src × DST ALPHA, dst × ZERO)");

        ShowQuad();
    }

    /// <summary>Reads the panel into the quads and settings, pushing values back when a button rewrites them.</summary>
    private void Poll()
    {
        for (int i = 0; i < 2; i++)
        {
            if (_ui.Clicked(_pick[i]))
            {
                _selected = i;
                ShowQuad();
            }
        }

        for (int i = 0; i < Presets.Length; i++)
        {
            if (_ui.Clicked(_presets[i]))
            {
                Selected.Blend = Presets[i].Blend;
                ShowQuad();
            }
        }

        _ui.PollSteppers();

        Quad quad = Selected;
        ColorWrite write = ColorWrite.None;
        for (int i = 0; i < 4; i++)
        {
            if (_write[i].GetComponent<Toggle>().Value)
                write |= (ColorWrite)(1 << i);
        }

        quad.Blend = quad.Blend with { Enabled = _enabled.GetComponent<Toggle>().Value, Write = write };
        quad.Ramp = _ramp.GetComponent<Toggle>().Value;
        quad.Colour = new Color(Slider(_colour[0]), Slider(_colour[1]), Slider(_colour[2]), Slider(_colour[3]));
        _constant = new Color(Slider(_constants[0]), Slider(_constants[1]), Slider(_constants[2]), Slider(_constants[3]));
        _backdropAlpha = Slider(_backdropAlphaSlider);
        _showAlpha = _alphaView.GetComponent<Toggle>().Value;

        _ui.ShowSteppers();
        UiText.Set(_colourEquation, "rgb = " + Equation(quad.Blend.Enabled, quad.Blend.ColorOp, quad.Blend.SrcColor, quad.Blend.DstColor, "rgb"));
        UiText.Set(_alphaEquation, "  a = " + Equation(quad.Blend.Enabled, quad.Blend.AlphaOp, quad.Blend.SrcAlpha, quad.Blend.DstAlpha, "a"));
    }

    private static float Slider(Entity slider) => LabPanel.Value(slider);

    /// <summary>Puts the selected quad's values on the panel's controls.</summary>
    private void ShowQuad()
    {
        Quad quad = Selected;
        LabPanel.ShowChoice(_pick, _selected);

        _enabled.GetComponent<Toggle>().Value = quad.Blend.Enabled;
        for (int i = 0; i < 4; i++)
            _write[i].GetComponent<Toggle>().Value = (quad.Blend.Write & (ColorWrite)(1 << i)) != 0;
        _ramp.GetComponent<Toggle>().Value = quad.Ramp;
        for (int i = 0; i < 4; i++)
            _colour[i].GetComponent<UiSlider>().Value = quad.Colour[i];
    }

    private static string OpName(BlendOp op) => op switch
    {
        BlendOp.Add => "ADD",
        BlendOp.Subtract => "SUBTRACT  (src − dst)",
        BlendOp.ReverseSubtract => "REVERSE SUBTRACT  (dst − src)",
        BlendOp.Min => "MIN  (factors ignored)",
        _ => "MAX  (factors ignored)",
    };

    private static string FactorName(BlendFactor factor) => factor switch
    {
        BlendFactor.Zero => "ZERO",
        BlendFactor.One => "ONE",
        BlendFactor.SrcColor => "SRC COLOUR",
        BlendFactor.OneMinusSrcColor => "1 − SRC COLOUR",
        BlendFactor.DstColor => "DST COLOUR",
        BlendFactor.OneMinusDstColor => "1 − DST COLOUR",
        BlendFactor.SrcAlpha => "SRC ALPHA",
        BlendFactor.OneMinusSrcAlpha => "1 − SRC ALPHA",
        BlendFactor.DstAlpha => "DST ALPHA",
        BlendFactor.OneMinusDstAlpha => "1 − DST ALPHA",
        BlendFactor.ConstantColor => "CONSTANT",
        BlendFactor.OneMinusConstantColor => "1 − CONSTANT",
        _ => "SRC ALPHA SATURATE  min(srcA, 1 − dstA)",
    };

    private static string Short(BlendFactor factor, string channel) => factor switch
    {
        BlendFactor.SrcColor => "src." + channel,
        BlendFactor.OneMinusSrcColor => "(1-src." + channel + ")",
        BlendFactor.DstColor => "dst." + channel,
        BlendFactor.OneMinusDstColor => "(1-dst." + channel + ")",
        BlendFactor.SrcAlpha => "src.a",
        BlendFactor.OneMinusSrcAlpha => "(1-src.a)",
        BlendFactor.DstAlpha => "dst.a",
        BlendFactor.OneMinusDstAlpha => "(1-dst.a)",
        BlendFactor.ConstantColor => "k." + channel,
        BlendFactor.OneMinusConstantColor => "(1-k." + channel + ")",
        BlendFactor.SrcAlphaSaturate => "sat",
        _ => "",
    };

    private static string Term(string side, BlendFactor factor, string channel) => factor switch
    {
        BlendFactor.Zero => "0",
        BlendFactor.One => side + "." + channel,
        _ => side + "." + channel + "*" + Short(factor, channel),
    };

    private static string Equation(bool enabled, BlendOp op, BlendFactor src, BlendFactor dst, string channel)
    {
        if (!enabled)
            return "src." + channel + "   (no blending)";

        string s = Term("src", src, channel), d = Term("dst", dst, channel);
        return op switch
        {
            BlendOp.Add => s + " + " + d,
            BlendOp.Subtract => s + " - " + d,
            BlendOp.ReverseSubtract => d + " - " + s,
            BlendOp.Min => "min(src." + channel + ", dst." + channel + ")",
            _ => "max(src." + channel + ", dst." + channel + ")",
        };
    }

    private sealed class Quad
    {
        public CustomBlend Blend = CustomBlend.Mix;
        public Color Colour;
        public bool Ramp;
    }

    // ---- stage --------------------------------------------------------------

    public ILabStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(this, parent, area);

    private sealed class Stage : ILabStage
    {
        private static readonly Color[] Bands =
        [
            Colors.Black, Colors.White, new(0.5f, 0.5f, 0.5f), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1),
            new(1, 1, 0), new(0, 1, 1), new(1, 0, 1),
        ];

        /// <summary>Replaces what's there, alpha included, so the backdrop's alpha is exactly what was asked.</summary>
        private static readonly CanvasMaterial Replace = new("") { Custom = CustomBlend.Mix with { Enabled = false } };

        /// <summary>rgb = white × dst.a: the target's alpha as grey, alpha left alone.</summary>
        private static readonly CanvasMaterial AlphaAsGrey = new("")
        {
            Custom = CustomBlend.Mix with
            {
                SrcColor = BlendFactor.DstAlpha, DstColor = BlendFactor.Zero,
                Write = ColorWrite.R | ColorWrite.G | ColorWrite.B,
            },
        };

        /// <summary>Writes alpha 1 and nothing else, so the stage leaves the target opaque for the window.</summary>
        private static readonly CanvasMaterial OpaqueAlpha = new("")
        {
            Custom = CustomBlend.Mix with { Enabled = false, Write = ColorWrite.A },
        };

        private readonly BlendLab _lab;
        private readonly Rect2 _area;
        private readonly Handle _backdrop, _alphaView, _opaque;
        private readonly Handle[] _quads = new Handle[2];
        private readonly CustomBlend?[] _shown = new CustomBlend?[2];

        public Stage(BlendLab lab, Handle parent, Rect2 area)
        {
            _lab = lab;
            _area = area;
            _backdrop = Item(parent, Replace);
            _quads[0] = Item(parent, null);
            _quads[1] = Item(parent, null);
            _alphaView = Item(parent, AlphaAsGrey);
            _opaque = Item(parent, OpaqueAlpha);
            CanvasServer.Current.AddRect(_opaque, area, Colors.White);
            CanvasServer.Current.AddRect(_alphaView, area, Colors.White);
        }

        private static Handle Item(Handle parent, CanvasMaterial? material)
        {
            Handle item = CanvasServer.Current.CreateItem();
            CanvasServer.Current.SetParent(item, parent);
            CanvasServer.Current.SetMaterial(item, material);
            return item;
        }

        public void Update(in LabFrame frame)
        {
            _lab.Poll();
            DrawBackdrop();
            Rect2 a = Square(new Vector2(-0.14f, -0.1f));
            Rect2 b = Square(new Vector2(0.14f, 0.1f));
            DrawQuad(0, a);
            DrawQuad(1, b);
            CanvasServer.Current.SetVisible(_alphaView, _lab._showAlpha);
        }

        private Rect2 Square(Vector2 offset)
        {
            float side = MathF.Min(_area.Size.X, _area.Size.Y) * 0.5f;
            Vector2 centre = _area.GetCenter() + offset * _area.Size;
            return new Rect2(centre - new Vector2(side, side) / 2, new Vector2(side, side));
        }

        private void DrawBackdrop()
        {
            ICanvasServer canvas = CanvasServer.Current;
            canvas.Clear(_backdrop);
            float alpha = _lab._backdropAlpha;
            switch (_lab._backdrop)
            {
                case 0:
                    float width = _area.Size.X / Bands.Length;
                    for (int i = 0; i < Bands.Length; i++)
                    {
                        var band = new Rect2(_area.Position + new Vector2(width * i, 0), new Vector2(width, _area.Size.Y));
                        canvas.AddRect(_backdrop, band, Bands[i] with { A = alpha });
                    }

                    break;
                case 1:
                    const float cell = 48;
                    for (float y = 0; y < _area.Size.Y; y += cell)
                    for (float x = 0; x < _area.Size.X; x += cell)
                    {
                        bool light = ((int)(x / cell) + (int)(y / cell)) % 2 == 0;
                        var size = new Vector2(MathF.Min(cell, _area.Size.X - x), MathF.Min(cell, _area.Size.Y - y));
                        float grey = light ? 0.75f : 0.25f;
                        canvas.AddRect(_backdrop, new Rect2(_area.Position + new Vector2(x, y), size), new Color(grey, grey, grey, alpha));
                    }

                    break;
                default:
                    float value = _lab._backdrop switch { 2 => 0f, 3 => 1f, _ => 0.5f };
                    canvas.AddRect(_backdrop, _area, new Color(value, value, value, alpha));
                    break;
            }
        }

        private void DrawQuad(int index, Rect2 rect)
        {
            Quad quad = _lab._quads[index];
            Handle item = _quads[index];
            CustomBlend blend = quad.Blend with { Constant = _lab._constant };
            if (_shown[index] != blend)
            {
                // A material's blend is fixed, so a change is a new material; pipelines are cached by blend.
                CanvasServer.Current.SetMaterial(item, new CanvasMaterial("") { Custom = blend });
                _shown[index] = blend;
            }

            CanvasServer.Current.Clear(item);
            Color colour = quad.Colour;
            Color left = quad.Ramp ? colour with { A = 0 } : colour;
            Vector2 p = rect.Position, s = rect.Size;
            CanvasServer.Current.AddPolygon(item,
                [p, p + new Vector2(s.X, 0), p + s, p + new Vector2(0, s.Y)],
                [left, colour, colour, left]);
        }

        public void Dispose()
        {
            foreach (Handle item in (Handle[])[_backdrop, _quads[0], _quads[1], _alphaView, _opaque])
                CanvasServer.Current.Free(item);
        }
    }
}
