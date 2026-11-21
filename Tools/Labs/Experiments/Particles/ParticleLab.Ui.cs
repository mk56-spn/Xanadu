using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Labs.Experiments.Particles;

internal sealed partial class ParticleLab
{
    /// <summary>Every press the panel's buttons make; the argument is a row or an emitter.</summary>
    private enum LabCommand
    {
        Pause,
        Step,
        Reset,
        Emitter,
        ModulePick,
        ModuleToggle,
        ModuleUp,
        ModuleDown,
        ModuleRemove,
    }

    private sealed partial class Stage
    {
        private const float SideWidth = 380;
        private const float EmitterWidth = 420;

        /// <summary>The room kept under the view for the readouts and the module card.</summary>
        private const float UnderView = 250;

        private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

        private IDisposable? _presses;
        private Custom _view = null!;
        private Button _pause = null!;
        private readonly Button[] _emitterTabs = new Button[ParticleScene.EmitterCount];
        private Column _emitterCard = null!, _moduleCard = null!, _worldCard = null!;
        private PooledList _modules = null!;
        private readonly List<ModuleRow> _moduleRows = new();
        private Meter _pool = null!;
        private Label _alive = null!, _flow = null!, _events = null!;
        private UiNode _body;

        private int _emitter;
        private int _module = 0;

        /// <summary>Presses and choices only change the scene; the panel is shown again from it in <see cref="ShowPending"/>, after the UI has run.</summary>
        private bool _reshow, _reveal, _restyleTabs;

        public void Build(UiNode body)
        {
            _body = body;
            body.Add(new Row(Fill, gap: Space.Xl)
            {
                new Column(width: SideWidth, gap: Space.Md, cross: Cross.Stretch)
                {
                    new Row(gap: Space.Md, cross: Cross.Center)
                    {
                        new Label("PARTICLES", UiType.Heading, ink: Ink.Accent, grow: 1),
                        Choice("", ParticlePresets.Names, 0, 170, LoadPreset),
                    },
                    new Label("State carries across frames on the GPU: a pool with free slots, a trail ring per particle, " +
                              "a field they write and read, and deaths that burst other emitters. Pick a preset, then change anything.",
                        UiType.Small, ink: Ink.Dim, wrap: true),
                    new Row(gap: Space.Sm, cross: Cross.Center)
                    {
                        (_pause = new Button("PAUSE", cmd: Cmd(LabCommand.Pause), tip: "Stop the simulation; the view keeps drawing")),
                        new Button("STEP", cmd: Cmd(LabCommand.Step), tip: "One frame on, at 1/60 s"),
                        new Button("RESET", tone: Tone.Danger, cmd: Cmd(LabCommand.Reset), tip: "Empty the pool and the field"),
                    },
                    (_worldCard = new Column(gap: Space.Sm, cross: Cross.Stretch)),
                    new Row(gap: Space.Sm, cross: Cross.Center)
                    {
                        new Label("MODULES", UiType.Section, ink: Ink.Accent, tip: "Every particle goes through these each frame, top to bottom"),
                        new Spacer(),
                        Choice("", ModuleKinds.Names, 0, 150, AddModule, "ADD ▼"),
                    },
                    (_modules = new PooledList(Control.Sm, Space.Xs, changed: FillModules, grow: 1)
                    {
                        Row = _ => [Remember(_moduleRows, new ModuleRow())],
                    }),
                },
                new Column(width: EmitterWidth, gap: Space.Md, cross: Cross.Stretch)
                {
                    new Row(gap: Space.Sm, cross: Cross.Center)
                    {
                        new Label("EMITTER", UiType.Section, ink: Ink.Accent, grow: 1),
                        Tab(0), Tab(1), Tab(2), Tab(3),
                    },
                    new Scroll(grow: 1, stretch: true)
                    {
                        (_emitterCard = new Column(gap: Space.Sm, cross: Cross.Stretch)),
                    },
                },
                new Column(Fill, gap: Space.Md, cross: Cross.Start)
                {
                    (_view = new Custom(p => p.Image(_shown, ViewSize()), tip: "The world, 1280 × 720. The pointer reaches it through POINTER modules")),
                    new Row(gap: Space.Lg, cross: Cross.Center)
                    {
                        new Label("POOL", UiType.Caption, ink: Ink.Dim),
                        (_pool = new Meter(0, size: new Vector2(160, Space.Sm), tip: "Share of the 262,144 slots in use")),
                        (_alive = new Label("", UiType.Small, width: 220, fit: true)),
                        (_flow = new Label("", UiType.Small, width: 260, fit: true)),
                        (_events = new Label("", UiType.Small, width: 220, fit: true)),
                    },
                    new Scroll(grow: 1, stretch: true)
                    {
                        (_moduleCard = new Column(gap: Space.Sm, cross: Cross.Start)),
                    },
                },
            });

            WireChoices();
            _presses = UiCommandEvents.OnPressed<LabCommand>(body.Entity.Store, Pressed);
            ShowScene();
        }

        private Button Tab(int index) =>
            _emitterTabs[index] = new Button(((char)('A' + index)).ToString(), width: Control.Md, cmd: Cmd(LabCommand.Emitter, index),
                tip: $"Emitter {(char)('A' + index)}");

        private static T Remember<T>(List<T> into, T spec)
        {
            into.Add(spec);
            return spec;
        }

        /// <summary>The view at 16:9, as big as the room right of the columns allows.</summary>
        private Vector2 ViewSize()
        {
            float width = _area.Size.X - SideWidth - EmitterWidth - 2 * Space.Xl;
            float height = _area.Size.Y - UnderView;
            float w = MathF.Floor(MathF.Max(160, MathF.Min(width, height * 16f / 9f)));
            return new Vector2(w, MathF.Floor(w * 9f / 16f));
        }

        private void FitView()
        {
            if (!_view.Node.IsNull)
                _view.Node.Size(ViewSize());
        }

        /// <summary>Shows what the last presses and edits changed.</summary>
        private void ShowPending()
        {
            Relabel();
            if (_reshow)
            {
                _reshow = false;
                _restyleTabs = false;
                ShowScene();
                if (_reveal)
                    UiLists.Reveal(_modules, _module);
                _reveal = false;
            }
            else if (_restyleTabs)
            {
                _restyleTabs = false;
                StyleTabs();
            }
        }

        private void StyleTabs()
        {
            _pause.Node.Chosen(_paused).SetText(_paused ? "PLAY" : "PAUSE");
            for (int i = 0; i < _emitterTabs.Length; i++)
            {
                UiNode tab = _emitterTabs[i].Node;
                if (i == _emitter)
                    tab.Chosen(true);
                else if (_scene.Emitters[i].On)
                    tab.Secondary();
                else
                    tab.Quiet();
            }
        }

        /// <summary>Everything the scene drives, shown again: after a preset, a reset or a change of emitter or module.</summary>
        private void ShowScene()
        {
            StyleTabs();
            Refill(_worldCard, WorldFields());
            Refill(_emitterCard, EmitterFields(_scene.Emitters[_emitter], _emitter));
            Refill(_moduleCard, ModuleFields());
            UiLists.SetCount(_modules, _scene.Modules.Count, keepPlace: true);
        }

        private void Pressed(LabCommand command, UiCommand press, Entity _)
        {
            int row = press.Arg + (_modules.Node.IsNull ? 0 : _modules.Node.Entity.GetComponent<UiList>().First);
            switch (command)
            {
            case LabCommand.Pause:
                _paused = !_paused;
                break;
            case LabCommand.Step:
                _paused = true;
                _stepOnce = true;
                break;
            case LabCommand.Reset:
                Restart();
                break;
            case LabCommand.Emitter:
                _emitter = press.Arg;
                break;
            case LabCommand.ModulePick:
                _module = row;
                break;
            case LabCommand.ModuleToggle when row < _scene.Modules.Count:
                _scene.Modules[row].On = !_scene.Modules[row].On;
                break;
            case LabCommand.ModuleUp when row > 0 && row < _scene.Modules.Count:
                (_scene.Modules[row - 1], _scene.Modules[row]) = (_scene.Modules[row], _scene.Modules[row - 1]);
                _module = row - 1;
                break;
            case LabCommand.ModuleDown when row + 1 < _scene.Modules.Count:
                (_scene.Modules[row + 1], _scene.Modules[row]) = (_scene.Modules[row], _scene.Modules[row + 1]);
                _module = row + 1;
                break;
            case LabCommand.ModuleRemove when row < _scene.Modules.Count:
                _scene.Modules.RemoveAt(row);
                _module = Math.Min(_module, _scene.Modules.Count - 1);
                break;
            default:
                return;
            }

            _reshow = true;
        }

        private void LoadPreset(int index)
        {
            _scene = ParticlePresets.All[index].Make();
            _emitter = 0;
            _module = _scene.Modules.Count > 0 ? 0 : -1;
            Restart();
            _reshow = true;
        }

        private void AddModule(int kind)
        {
            if (_scene.Modules.Count >= ParticleScene.MostModules)
                return;
            _scene.Modules.Add(new ModuleSettings(ModuleKinds.FromIndex(kind)));
            _module = _scene.Modules.Count - 1;
            _reshow = true;
            _reveal = true;
        }

        /// <summary>The module stack's shown rows, from the list's first on; the picked one lit.</summary>
        private void FillModules()
        {
            UiList view = _modules.Node.Entity.GetComponent<UiList>();
            for (int i = 0; i < view.Shown && i < _moduleRows.Count; i++)
            {
                int index = view.First + i;
                if (index >= _scene.Modules.Count)
                    continue;
                ModuleSettings module = _scene.Modules[index];
                ModuleRow row = _moduleRows[i];
                row.Toggle.Chosen(module.On).SetText(module.On ? "ON" : "OFF");
                row.Name.Chosen(index == _module).SetText($"{index + 1}  {ModuleKinds.Of(module.Kind).Name}");
                row.Name.Tooltip(ModuleKinds.Of(module.Kind).Tip);
            }
        }

        /// <summary>The last second's counts from the GPU: the pool's fill, the live, the flow in and out, the deaths waiting to burst.</summary>
        private void Readouts(float delta)
        {
            if (_pool.Node.IsNull)
                return;

            ParticleCounts c = _gpu.Counts;
            _rateClock += delta;
            _spawnedSum += c.Spawned;
            _diedSum += c.Died;
            if (_rateClock >= 1)
            {
                _spawnedPerSecond = (int)(_spawnedSum / _rateClock);
                _diedPerSecond = (int)(_diedSum / _rateClock);
                _rateClock = 0;
                _spawnedSum = _diedSum = 0;
            }

            UiMeters.Set(_pool.Node.Entity, (float)c.Alive / ParticleGpu.Capacity);
            _alive.Node.SetText($"{c.Alive:N0} alive · {c.Trails:N0} trailing");
            _flow.Node.SetText($"+{_spawnedPerSecond:N0}/s born · -{_diedPerSecond:N0}/s died");
            _events.Node.SetText($"{c.Events:N0} deaths bursting");
        }

        /// <summary>One row of the module stack: on or off, its name (pick it), up, down and remove.</summary>
        private sealed class ModuleRow() : UiSpec(grow: 1, stretch: true)
        {
            private readonly Button _toggle = new("OFF", UiType.Caption, cmd: Cmd(LabCommand.ModuleToggle), width: 52, tip: "Switch this module on or off");
            private readonly Button _name = new("", UiType.Label, cmd: Cmd(LabCommand.ModulePick), grow: 1, tip: "Edit it under the view");

            public UiNode Toggle => _toggle.Node;
            public UiNode Name => _name.Node;

            protected override UiNode Create(UiNode parent) => parent.Add(new Row(gap: Space.Xs, cross: Cross.Stretch)
            {
                _toggle,
                _name,
                new Button("▲", UiType.Caption, cmd: Cmd(LabCommand.ModuleUp), width: Control.Sm, tip: "Run it earlier"),
                new Button("▼", UiType.Caption, cmd: Cmd(LabCommand.ModuleDown), width: Control.Sm, tip: "Run it later"),
                new Button("✕", UiType.Caption, tone: Tone.Danger, cmd: Cmd(LabCommand.ModuleRemove), width: Control.Sm, tip: "Remove it"),
            }).Node;
        }
    }
}
