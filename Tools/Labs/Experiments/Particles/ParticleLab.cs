using MoonWorks.Graphics;
using Xanadu.Host.Tools;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Experiments.Particles;

/// <summary>
/// PARTICLES: a general particle system whose state carries over from frame to
/// frame on the GPU (<see cref="ParticleGpu"/>). Four emitters spawn onto
/// shapes; a stack of modules (forces, drag, noise, attractors, vortices, the
/// pointer, the field, bounds, speed limits) moves every particle each frame;
/// particles keep a ring of past positions for real trails, write into a
/// fading field the others can climb, descend or steer by, and can burst
/// another emitter when they die. Presets are only data for the same panel.
/// The panel is built from the UI tree's specs and value fields throughout.
/// </summary>
internal sealed partial class ParticleLab : ILabExperiment
{
    public string Name => "PARTICLES";

    public bool FullWidth => true;

    public string[] Notes => [];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(context, area);

    /// <summary>The tab: the panel round the view (<c>ParticleLab.Ui.cs</c>), and the show it drives.</summary>
    private sealed partial class Stage : IToolStage
    {
        /// <summary>The world the show runs in, whatever size the view is drawn at.</summary>
        public static readonly Vector2 World = new(1280, 720);

        private const TextureFormat Format = TextureFormat.R8G8B8A8Unorm;

        /// <summary>A frame longer than this is cut to it, so a hitch never throws the show.</summary>
        private const float LongestStep = 1f / 20;

        private readonly LabContext _context;
        private readonly ParticleGpu _gpu;
        private readonly Texture _target;
        private readonly Handle _shown;
        private Rect2 _area;

        private ParticleScene _scene = ParticlePresets.All[0].Make();
        private readonly float[] _owed = new float[ParticleScene.EmitterCount];
        private readonly float[] _sinceBurst = new float[ParticleScene.EmitterCount];
        private readonly uint[] _queued = new uint[ParticleScene.EmitterCount];

        private bool _paused, _stepOnce;
        private float _time;
        private uint _frame, _head;
        private SimGpu _computeSim, _drawSim;
        private bool _moving;

        /// <summary>Particle counts over the last second, for the per-second readouts.</summary>
        private float _rateClock;
        private int _spawnedSum, _diedSum, _spawnedPerSecond, _diedPerSecond;

        public Stage(LabContext context, Rect2 area)
        {
            _context = context;
            _area = area;
            _gpu = new ParticleGpu(context.Device, Format);
            _target = Texture.Create2D(context.Device, "particle view", (uint)World.X, (uint)World.Y, Format,
                TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
            _shown = context.Show(_target);
            Restart();
        }

        /// <summary>Empties the pool and the field, and lets every emitter's first burst go again.</summary>
        private void Restart()
        {
            _gpu.Clear();
            _time = 0;
            for (int i = 0; i < ParticleScene.EmitterCount; i++)
            {
                _owed[i] = 0;
                _sinceBurst[i] = 0;
                _queued[i] = _scene.Emitters[i].On ? _scene.Emitters[i].Burst : 0;
            }
        }

        public void Update(in ToolFrame frame)
        {
            _gpu.PollCounts();
            bool step = !_paused || _stepOnce;
            float dt = _stepOnce ? 1f / 60 : MathF.Min(frame.Delta, LongestStep) * _scene.World.TimeScale;
            _stepOnce = false;
            _moving = step && dt > 0;
            if (_moving)
                _time += dt;

            Pack(dt, Pointer(frame));
            ShowPending();
            Readouts(frame.Delta);
        }

        /// <summary>The pointer in the world's pixels, whether the button is down, and whether it is over the view at all.</summary>
        private Vector4 Pointer(in ToolFrame frame)
        {
            if (_view.Node.IsNull || !UiLayout.Settled(_view.Node.Entity))
                return Vector4.Zero;
            Rect2 box = UiLayout.Box(_view.Node.Entity);
            if (!box.HasPoint(frame.Mouse))
                return Vector4.Zero;
            Vector2 at = (frame.Mouse - box.GetCenter()) / box.Size * World;
            return new Vector4(at.X, at.Y, frame.Held ? 1 : 0, 1);
        }

        /// <summary>The settings block for this frame: what each emitter owes, the trail ring's head, the pointer and every module.</summary>
        private void Pack(float dt, Vector4 mouse)
        {
            WorldSettings world = _scene.World;
            var sim = new SimGpu
            {
                Counts = new UInt4(ParticleGpu.Capacity, (uint)Math.Min(_scene.Modules.Count, ParticleScene.MostModules), ParticleGpu.MostEvents, 0),
                Time = new Vector4(_time, _moving ? dt : 0, World.X, World.Y),
                Mouse = mouse,
                Field = new Vector4(world.FieldFade, Math.Clamp(world.FieldSpread, 0, 1), ParticleGpu.FieldWidth, ParticleGpu.FieldHeight),
                Look = new Vector4(world.FieldGain, 0, 0, 0),
                Tint = SimGpu.Colour(world.FieldTint),
            };

            uint total = 0;
            for (int i = 0; i < ParticleScene.EmitterCount; i++)
            {
                uint spawns = _moving ? Owed(i, dt, ParticleGpu.MostSpawnsPerFrame - total) : 0;
                sim.Emitters[i] = SimGpu.Pack(_scene.Emitters[i], spawns, total);
                total += spawns;
            }

            for (int m = 0; m < (int)sim.Counts.Y; m++)
                sim.Modules[m] = SimGpu.Pack(_scene.Modules[m]);

            bool sample = _moving && _frame % Math.Max(1u, world.TrailEvery) == 0;
            if (_moving)
                _frame++;
            if (sample)
                _head = (_head + 1) % ParticleGpu.TrailRing;
            sim.Frame = new UInt4(_frame, sample ? _head : 0xffffffffu, ParticleGpu.TrailRing, total);
            _computeSim = sim;
            _drawSim = sim;
            _drawSim.Frame.Y = _head;
        }

        /// <summary>How many emitter <paramref name="index"/> spawns this frame: its rate's share, plus bursts, never past <paramref name="room"/>.</summary>
        private uint Owed(int index, float dt, uint room)
        {
            EmitterSettings e = _scene.Emitters[index];
            if (!e.On)
                return 0;

            _owed[index] += MathF.Max(0, e.Rate) * dt;
            if (e.BurstEvery > 0)
            {
                _sinceBurst[index] += dt;
                if (_sinceBurst[index] >= e.BurstEvery)
                {
                    _sinceBurst[index] -= e.BurstEvery;
                    _queued[index] += e.Burst;
                }
            }

            uint fromRate = (uint)MathF.Min(_owed[index], room);
            _owed[index] -= fromRate;
            // Never let a rate the pool can't take pile up into one huge frame later.
            _owed[index] = MathF.Min(_owed[index], ParticleGpu.MostSpawnsPerFrame);
            uint fromBurst = Math.Min(_queued[index], room - fromRate);
            _queued[index] -= fromBurst;
            return fromRate + fromBurst;
        }

        public void Render(CommandBuffer commands)
        {
            _gpu.Step(commands, _computeSim, _moving);
            _gpu.Draw(commands, _target, _drawSim, _scene.World.Background, _scene.World.Additive);
        }

        public void Resized(Rect2 area)
        {
            _area = area;
            FitView();
        }

        public void Dispose()
        {
            _presses?.Dispose();
            _gpu.Dispose();
            _context.Hide(_shown);
            _target.Dispose();
        }
    }
}
