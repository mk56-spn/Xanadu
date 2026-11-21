using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;

namespace Xanadu.Labs.Experiments.Particles;

internal sealed partial class ParticleLab
{
    private sealed partial class Stage
    {
        private static readonly UiValueShape F = UiValueShape.Float;
        private static readonly UiValueShape F2 = UiValueShape.Vector(2);
        private static readonly UiValueShape Whole = UiValueShape.UInt;
        private static readonly UiValueShape Switch = UiValueShape.Bool;
        private static readonly UiValueShape Rgba = UiValueShape.ColourValue;

        private static readonly string[] Shapes = ["POINT", "LINE", "CIRCLE", "DISC", "BOX", "RING"];
        private static readonly string[] Looks = ["GLOW", "DOT", "RING", "SPARK", "SQUARE"];
        private static readonly string[] Children = ["NONE", "A", "B", "C", "D"];

        /// <summary>Value fields built but not yet bound: bound once the tree they are in exists.</summary>
        private readonly List<(ValueField Spec, Action<float[]> Set)> _knobs = new();

        /// <summary>Dropdowns built but not yet bound, and the label each shows once chosen.</summary>
        private readonly List<(Custom Spec, string[] Options, Action<int> Chosen, string? Fixed)> _choices = new();

        /// <summary>Button labels to change once the UI has run (a choice lands mid-playback).</summary>
        private readonly List<(Entity Button, string Text)> _relabel = new();

        /// <summary>Which folding sections were open, by title, so a rebuild keeps them as they were.</summary>
        private readonly Dictionary<string, bool> _open = new()
        {
            ["COLOUR"] = false,
            ["FIELD COLOUR"] = false,
        };

        private readonly List<(string Title, Entity Section)> _sections = new();

        // ---- the cards ----------------------------------------------------------------

        private IEnumerable<UiSpec> WorldFields()
        {
            WorldSettings w = _scene.World;
            const float half = (SideWidth - Space.Md) / 2;
            yield return Section("WORLD", SideWidth,
                Pair(Knob("time scale", F, [w.TimeScale], v => w.TimeScale = v[0], (0, 3), "1 is real time; 0 holds still", half),
                    Knob("additive", Switch, [w.Additive ? 1 : 0], v => w.Additive = v[0] > 0.5f, (0, 1), "Add light (glows) or lay over (smoke, snow)", half)),
                Pair(Knob("trail every", Whole, [w.TrailEvery], v => w.TrailEvery = (uint)v[0], (1, 8), "Frames between trail samples: more makes the same 12 samples reach further back", half),
                    Knob("field shown", F, [w.FieldGain], v => w.FieldGain = v[0], (0, 4), "How bright the field draws under the particles; 0 hides it", half)),
                Pair(Knob("field fade", F, [w.FieldFade], v => w.FieldFade = v[0], (0, 5), "How fast marks in the field fade, per second", half),
                    Knob("field spread", F, [w.FieldSpread], v => w.FieldSpread = v[0], (0, 1), "How much each cell blurs into its neighbours each frame", half)));
            yield return Section("FIELD COLOUR", SideWidth,
                Knob("tint", Rgba, Channels(w.FieldTint), v => w.FieldTint = Colour(v), (0, 1), "The field's colour", SideWidth));
        }

        private IEnumerable<UiSpec> EmitterFields(EmitterSettings e, int index)
        {
            const float half = (EmitterWidth - Space.Md) / 2;
            yield return Section("SPAWN", EmitterWidth,
                Pair(Knob("on", Switch, [e.On ? 1 : 0], v => { e.On = v[0] > 0.5f; _restyleTabs = true; }, (0, 1), "Spawns at all, even as a child", half),
                    Choice("shape", Shapes, (int)e.Shape, half, i => e.Shape = (EmitterShape)i)),
                Pair(Knob("at", F2, [e.Position.X, e.Position.Y], v => e.Position = new Vector2(v[0], v[1]), (-640, 640), "px from the middle, y down", half),
                    Knob("size", F2, [e.Size.X, e.Size.Y], v => e.Size = new Vector2(v[0], v[1]), (0, 1400), "LINE: length. CIRCLE, DISC: radius. BOX: width, height. RING: inner, outer radius", half)),
                Pair(Knob("turn", F, [e.Turn], v => e.Turn = v[0], (-180, 180), "Degrees the shape is turned", half),
                    Knob("rate", F, [e.Rate], v => e.Rate = v[0], (0, 20000), "Particles a second", half)),
                Pair(Knob("burst", Whole, [e.Burst], v => e.Burst = (uint)v[0], (0, 200000), "Particles at once", half),
                    Knob("burst every", F, [e.BurstEvery], v => e.BurstEvery = v[0], (0, 10), "Seconds between bursts; 0 bursts once after RESET", half)));
            yield return Section("MOTION", EmitterWidth,
                Pair(Knob("direction", F, [e.Direction], v => e.Direction = v[0], (-180, 180), "Degrees they leave in: 0 right, -90 up", half),
                    Knob("spread", F, [e.Spread], v => e.Spread = v[0], (0, 360), "Degrees either side, all told; 360 is every way", half)),
                Pair(Knob("speed", F2, [e.Speed.X, e.Speed.Y], v => e.Speed = new Vector2(v[0], v[1]), (0, 1500), "Least and most px/s", half),
                    Knob("outward", F, [e.Outward], v => e.Outward = v[0], (0, 1), "Bends the direction out from the shape's middle", half)),
                Pair(Knob("tangent", F, [e.Tangent], v => e.Tangent = v[0], (-1, 1), "Share of the speed sent round the middle instead: orbits", half),
                    Knob("inherit", F, [e.Inherit], v => e.Inherit = v[0], (0, 1), "As a child: share of the parent's velocity kept", half)));
            yield return Section("LIFE AND LOOK", EmitterWidth,
                Pair(Knob("life", F2, [e.Life.X, e.Life.Y], v => e.Life = new Vector2(v[0], v[1]), (0.05f, 30), "Least and most seconds", half),
                    Knob("size", F2, [e.SizeRange.X, e.SizeRange.Y], v => e.SizeRange = new Vector2(v[0], v[1]), (0, 120), "px across at birth and at death", half)),
                Pair(Knob("jitter", F, [e.Jitter], v => e.Jitter = v[0], (0, 1), "How much sizes differ", half),
                    Knob("stretch", F, [e.Stretch], v => e.Stretch = v[0], (0, 0.1f), "Seconds of travel each is drawn along: streaks", half)),
                Pair(Choice("look", Looks, (int)e.Look, half, i => e.Look = (ParticleLook)i),
                    Knob("fade in", F, [e.FadeIn], v => e.FadeIn = v[0], (0, 3), "Seconds to come up from clear", half)));
            yield return Section("COLOUR", EmitterWidth,
                Pair(Knob("born", Rgba, Channels(e.Born), v => e.Born = Colour(v), (0, 1), "Colour at birth", half),
                    Knob("dies", Rgba, Channels(e.Dies), v => e.Dies = Colour(v), (0, 1), "Colour at death; its alpha fades them out", half)),
                Knob("hue jitter", F, [e.HueJitter], v => e.HueJitter = v[0], (0, 1), "Turns of hue each may be off by", half));
            yield return Section("TRAIL, FIELD, DEATH", EmitterWidth,
                Pair(Knob("trail", Whole, [e.Trail], v => e.Trail = (uint)v[0], (0, ParticleGpu.TrailRing), "Past positions drawn behind each; 0 or 1 is none", half),
                    Knob("trail width", F, [e.TrailWidth], v => e.TrailWidth = v[0], (0, 2), "Share of the particle's size", half)),
                Pair(Knob("deposit", F, [e.Deposit], v => e.Deposit = v[0], (0, 20), "Field each particle leaves a second", half),
                    Choice("on death burst", Children, e.Child + 1, half, i => e.Child = i - 1)),
                Knob("children", Whole, [e.Children], v => e.Children = (uint)v[0], (0, 1000), "How many of that emitter each death bursts", half));
        }

        private IEnumerable<UiSpec> ModuleFields()
        {
            if (_module < 0 || _module >= _scene.Modules.Count)
            {
                yield return new Label("Pick a module in the stack, or ADD one.", UiType.Body, ink: Ink.Dim);
                yield break;
            }

            ModuleSettings m = _scene.Modules[_module];
            ModuleKindInfo info = ModuleKinds.Of(m.Kind);
            const float width = 160;
            yield return new Row(gap: Space.Md, cross: Cross.Center)
            {
                new Label($"{_module + 1}  {info.Name}", UiType.Section, ink: Ink.Accent),
                new Label(info.Tip, UiType.Small, ink: Ink.Dim),
            };

            var row = new Row(gap: Space.Md, cross: Cross.Start);
            if (info.Modes.Length > 0)
                row.Add(Choice("mode", info.Modes, m.Mode, width, i => m.Mode = i));
            row.Add(Knob("emitters", UiValueShape.Vector(4, UiScalar.Bool), Bits(m.Mask),
                v => m.Mask = (v[0] > 0.5f ? 1u : 0) | (v[1] > 0.5f ? 2u : 0) | (v[2] > 0.5f ? 4u : 0) | (v[3] > 0.5f ? 8u : 0),
                (0, 1), "Which emitters it reaches: X is A, Y B, Z C, W D", width));
            foreach (ModuleParam p in info.Params)
            {
                ModuleParam param = p;
                float[] now = m.Values.AsSpan(param.Slot, param.Shape.Count).ToArray();
                row.Add(Knob(param.Label, param.Shape, now, v => v.AsSpan(0, param.Shape.Count).CopyTo(m.Values.AsSpan(param.Slot)), param.Range, param.Tip, width));
            }

            yield return row;
        }

        // ---- building blocks -------------------------------------------------------

        /// <summary>A value field bound to the scene once built: every edit, drags included, lands straight away.</summary>
        private ValueField Knob(string label, UiValueShape shape, float[] now, Action<float[]> set, (float, float) range, string tip, float width)
        {
            var spec = new ValueField(label, shape, now, width, range, tip);
            _knobs.Add((spec, set));
            return spec;
        }

        /// <summary>A captioned dropdown; its button shows what is chosen, or <paramref name="fixedText"/> always.</summary>
        private UiSpec Choice(string caption, string[] options, int selected, float width, Action<int> chosen, string? fixedText = null)
        {
            selected = Math.Clamp(selected, 0, options.Length - 1);
            var drop = new Custom(p => p.Dropdown(fixedText ?? $"{options[selected]} ▼", options, selected, new Vector2(width, Control.Xs)).Button);
            _choices.Add((drop, options, chosen, fixedText));
            return caption.Length == 0
                ? drop
                : new Column(gap: Space.Xs, cross: Cross.Start) { new Label(caption, UiType.Caption, ink: Ink.Accent), drop };
        }

        private static Row Pair(UiSpec a, UiSpec b) => new(gap: Space.Md, cross: Cross.Start) { a, b };

        /// <summary>A folding section, open or shut as it was last left.</summary>
        private Custom Section(string title, float width, params UiSpec[] children) => new(p =>
        {
            UiNode section = p.Collapsible(title, width, out UiNode body, _open.GetValueOrDefault(title, true), Space.Sm);
            foreach (UiSpec child in children)
                body.Add(child);
            _sections.Add((title, section.Entity));
            return section;
        });

        /// <summary>Empties a card and builds it again from <paramref name="specs"/>, then binds what it made.</summary>
        private void Refill(Column card, IEnumerable<UiSpec> specs)
        {
            Entity root = card.Node.Entity;
            for (int i = _sections.Count - 1; i >= 0; i--)
            {
                (string title, Entity section) = _sections[i];
                if (section.IsNull || !IsUnder(section, root))
                    continue;
                _open[title] = UiCollapsibleControl.IsExpanded(section);
                _sections.RemoveAt(i);
            }

            var children = new Entity[root.ChildCount];
            root.ChildEntities.ToArray(children);
            foreach (Entity child in children)
                UiLayout.DeleteSubtree(child);
            foreach (UiSpec spec in specs)
                card.Node.Add(spec);
            WireChoices();
        }

        private static bool IsUnder(Entity entity, Entity root)
        {
            for (Entity at = entity; !at.IsNull; at = at.Parent)
                if (at == root)
                    return true;
            return false;
        }

        /// <summary>Binds every value field and dropdown built since the last call.</summary>
        private void WireChoices()
        {
            foreach ((ValueField spec, Action<float[]> set) in _knobs)
                spec.Node.Entity.OnValueChanged((values, _) => set(values));
            _knobs.Clear();

            foreach ((Custom spec, string[] options, Action<int> chosen, string? fixedText) in _choices)
            {
                Entity button = spec.Node.Entity;
                button.OnComponentChanged += change =>
                {
                    if (change.Type != typeof(DropdownChanged) || change.Action == ComponentChangedAction.Remove)
                        return;
                    int value = Math.Clamp(change.Component<DropdownChanged>().Value, 0, options.Length - 1);
                    chosen(value);
                    if (fixedText is null)
                        _relabel.Add((button, $"{options[value]} ▼"));
                };
            }

            _choices.Clear();
        }

        /// <summary>Gives chosen dropdowns their new labels, after the UI has run.</summary>
        private void Relabel()
        {
            foreach ((Entity button, string text) in _relabel)
                if (!button.IsNull)
                    UiNode.Wrap(button).SetText(text);
            _relabel.Clear();
        }

        private static float[] Channels(Color c) => [c.R, c.G, c.B, c.A];

        private static Color Colour(float[] v) => new(v[0], v[1], v[2], v[3]);

        private static float[] Bits(uint mask) => [mask & 1, (mask >> 1) & 1, (mask >> 2) & 1, (mask >> 3) & 1];
    }
}
