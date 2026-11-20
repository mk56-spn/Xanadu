using System;
using System.Collections.Generic;
using MoonWorks.Input;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A blue body under gravity on a floor. Holding Space fires a booster from
/// its feet (thrust up, particles out the bottom); letting go cuts it.
/// Left / right move it at one fixed speed.
/// </summary>
internal sealed class BoosterLab : ILabExperiment
{
    public string Name => "BOOSTER";

    public string[] Notes =>
    [
        "Blue body under gravity.",
        "Hold Space: booster on your feet",
        "pushes you up; let go to cut it.",
        "Lighting it leaves a fading ring",
        "where your feet were.",
        "Left / right arrows: move at one",
        "fixed speed; let go and the speed",
        "bleeds off. Each press kicks sparks",
        "out the back, angled down, with a",
        "small side-on ring.",
        "S: beam the way you face (held).",
        "D: sweep, an arc swung top to",
        "bottom on your facing side.",
        "F: pulse, a ring out of the body.",
        "All three throw particles.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : IToolStage
    {

        private const float BodyWidth = 40f;      // 1 × 2.5, like the play body
        private const float BodyHeight = 100f;
        private const float FloorHeight = 24f;

        private const float Gravity = 1800f;      // px / s²
        private const float Thrust = 2600f;       // px / s², up, while boosting
        private const float MaxRise = 600f;       // px / s
        private const float MaxFall = 450f;       // px / s
        private const float RunSpeed = 400f;      // px / s, the single sideways speed
        private const float Accel = 3000f;        // px / s², toward run speed while a direction is held
        private const float Bleed = 350f;         // px / s², sideways slow-down with no direction held

        private const float EmitRate = 240f;      // particles / s while boosting
        private const float ParticleLife = 0.45f; // s
        private const float ParticleSpeed = 500f; // px / s, out of the feet
        private const float ParticleSpread = 0.35f; // radians either side of straight down
        private const float ParticleSize = 6f;

        private const float RingLife = 0.5f;      // s
        private const float RingWidth = 70f;      // starting oval, px
        private const float RingHeight = 18f;
        private const float RingGrow = 1.8f;      // size at the end of its life, × start
        private const int RingSegments = 40;

        private readonly List<Ring> _rings = [];
        private bool _wasBoosting;

        private const int SparkCount = 18;
        private const float SparkLife = 0.3f;     // s
        private const float SparkSpeed = 650f;    // px / s
        private const float SparkDrop = 0.3f;     // radians below horizontal
        private const float SparkSpread = 0.15f;  // radians either side
        private const float SideRingScale = 0.5f; // side ring vs the booster ring

        private float _lastSide;
        private float _facing = 1f;               // last direction pressed

        // S: a beam out the way you face, on while held, fading after.
        private const float BeamLength = 900f;
        private const float BeamWidth = 10f;
        private const float BeamFade = 0.15f;     // s after letting go
        private float _beam;                      // 0 off … 1 on

        // D: a sweep, an arc swung from overhead to underfoot on the facing side.
        private const float SweepRadius = 130f;
        private const float SweepTime = 0.08f;    // s to swing
        private const float SweepFade = 0.2f;     // s for the trail to go after
        private const int SweepSegments = 24;
        private float _sweepAge = -1f;            // < 0: none
        private float _sweepSide = 1f;

        // F: a pulse, a ring grown out of the body.
        private const float PulseRadius = 260f;
        private const float PulseLife = 0.4f;
        private const int PulseSegments = 48;
        private readonly List<float> _pulses = [];

        private Handle _fx;

        private struct Ring
        {
            public Vector2 Centre;
            public float Age;
            public float Width;
            public float Height;
        }

        private readonly Handle _back;
        private readonly Handle _body;
        private readonly Handle _particles;
        private readonly Rect2 _area;
        private readonly List<Particle> _live = [];
        private readonly Random _random = new(1);

        private Vector2 _position;               // feet, centre-bottom of the body
        private Vector2 _velocity;
        private float _emitCarry;
        private float _onFor;                     // s the booster has been held
        private float _offFor = 1f;               // s since it was let go

        private const float EmitEaseOut = 0.08f;  // s for emission to taper off after letting go
        private const float WarmUp = 0.3f;        // s for the flame to go from white to yellow

        private struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public Color Tint;
        }

        public Stage(Handle parent, Rect2 area)
        {
            _area = area;
            var canvas = CanvasServer.Current;
            _back = canvas.CreateItem();
            canvas.SetParent(_back, parent);
            canvas.AddRect(_back, area, Palette.Stage);
            canvas.AddRect(_back, new Rect2(area.Position.X, Floor, area.Size.X, FloorHeight), Palette.Muted);

            _fx = canvas.CreateItem();
            canvas.SetParent(_fx, _back);

            _particles = canvas.CreateItem();
            canvas.SetParent(_particles, _back);

            _body = canvas.CreateItem();
            canvas.SetParent(_body, _back);
            canvas.AddRect(_body, new Rect2(-BodyWidth * 0.5f, -BodyHeight, BodyWidth, BodyHeight), Palette.PrimarySoft);

            _position = new(area.Position.X + area.Size.X * 0.5f, Floor);
            Place();
        }

        private float Floor => _area.Position.Y + _area.Size.Y - FloorHeight;
        private float Ceiling => _area.Position.Y + BodyHeight;
        private float Left => _area.Position.X + BodyWidth * 0.5f;
        private float Right => _area.Position.X + _area.Size.X - BodyWidth * 0.5f;

        private void Place() =>
            CanvasServer.Current.SetTransform(_body, new Transform2D(0f, _position));

        public void Update(in ToolFrame frame)
        {
            Keyboard? keys = frame.Inputs?.Keyboard;
            bool boost = keys?.IsDown(KeyCode.Space) ?? false;
            float side = 0f;
            if (keys?.IsDown(KeyCode.Left) ?? false) side -= 1f;
            if (keys?.IsDown(KeyCode.Right) ?? false) side += 1f;

            if (boost && !_wasBoosting)
                _rings.Add(new Ring { Centre = _position, Width = RingWidth, Height = RingHeight });
            _wasBoosting = boost;

            // A fresh direction press kicks sparks out the trailing side, angled down, with a small ring.
            if (side != 0f && side != _lastSide)
                Kick(side);
            _lastSide = side;
            if (side != 0f)
                _facing = side;

            float dt = frame.Delta;
            bool beam = keys?.IsDown(KeyCode.S) ?? false;
            _beam = beam ? 1f : MathF.Max(0f, _beam - dt / BeamFade);
            if (beam)
                BeamSparks(dt);

            if (keys?.IsPressed(KeyCode.D) ?? false)
            {
                _sweepAge = 0f;
                _sweepSide = _facing;
            }
            else if (_sweepAge >= 0f)
            {
                if (_sweepAge <= SweepTime)
                    SweepSparks(_sweepAge, MathF.Min(_sweepAge + dt, SweepTime));
                _sweepAge += dt;
                if (_sweepAge > SweepTime + SweepFade)
                    _sweepAge = -1f;
            }

            if (keys?.IsPressed(KeyCode.F) ?? false)
            {
                _pulses.Add(0f);
                PulseSparks();
            }
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                _pulses[i] += dt;
                if (_pulses[i] >= PulseLife)
                    _pulses.RemoveAt(i);
            }

            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                Ring ring = _rings[i];
                ring.Age += frame.Delta;
                if (ring.Age >= RingLife)
                    _rings.RemoveAt(i);
                else
                    _rings[i] = ring;
            }

            // Real frame time, capped so a hitch doesn't fling everything.
            Advance(boost, side, MathF.Min(frame.Delta, 1f / 30f));

            Place();
            DrawParticles();
            DrawFx();
        }

        private void Advance(bool boost, float side, float step)
        {
            if (side != 0f)
            {
                float target = side * RunSpeed;
                float change = Math.Clamp(target - _velocity.X, -Accel * step, Accel * step);
                _velocity.X += change;
            }
            else
                _velocity.X = MathF.Sign(_velocity.X) * MathF.Max(MathF.Abs(_velocity.X) - Bleed * step, 0f);
            _velocity.Y += (Gravity - (boost ? Thrust : 0f)) * step;
            _velocity.Y = Math.Clamp(_velocity.Y, -MaxRise, MaxFall);
            _position += _velocity * step;

            if (_position.X <= Left || _position.X >= Right)
                _velocity.X = 0f;
            _position.X = Math.Clamp(_position.X, Left, Right);
            if (_position.Y >= Floor)
            {
                _position.Y = Floor;
                _velocity.Y = MathF.Min(_velocity.Y, 0f);
            }
            if (_position.Y <= Ceiling)
            {
                _position.Y = Ceiling;
                _velocity.Y = MathF.Max(_velocity.Y, 0f);
            }

            if (boost)
            {
                _onFor += step;
                _offFor = 0f;
            }
            else
            {
                _onFor = 0f;
                _offFor += step;
            }

            // Full rate while held; a short linear taper once let go.
            float rate = boost ? 1f : MathF.Max(0f, 1f - _offFor / EmitEaseOut);
            if (rate > 0f)
            {
                _emitCarry += EmitRate * rate * step;
                while (_emitCarry >= 1f)
                {
                    _emitCarry -= 1f;
                    Emit();
                }
            }
            else
                _emitCarry = 0f;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Particle p = _live[i];
                p.Age += step;
                if (p.Age >= p.Life)
                {
                    _live.RemoveAt(i);
                    continue;
                }
                p.Position += p.Velocity * step;
                if (p.Position.Y > Floor)
                {
                    p.Position.Y = Floor;
                    p.Velocity.Y *= -0.2f; // splash off the floor
                }
                _live[i] = p;
            }
        }

        // White on ignition, warming to yellow; tapering particles keep the last tint.
        private Color _lastFlame = new(1f, 1f, 1f);

        private Color FlameTint()
        {
            if (_onFor > 0f)
                _lastFlame = new Color(1f, 1f, 1f).Lerp(Palette.Accent, MathF.Min(_onFor / WarmUp, 1f));
            return _lastFlame;
        }

        private void Emit()
        {
            float angle = MathF.PI * 0.5f + ((float)_random.NextDouble() * 2f - 1f) * ParticleSpread;
            float speed = ParticleSpeed * (0.7f + 0.6f * (float)_random.NextDouble());
            float x = ((float)_random.NextDouble() - 0.5f) * BodyWidth * 0.8f;
            _live.Add(new Particle
            {
                Life = ParticleLife,
                Tint = FlameTint(),
                Position = _position + new Vector2(x, 0f),
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed + new Vector2(_velocity.X, 0f) * 0.5f,
            });
        }

        private void Kick(float side)
        {
            // Trailing side, low on the body.
            Vector2 from = _position + new Vector2(-side * BodyWidth * 0.5f, -BodyHeight * 0.25f);
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = SparkDrop + ((float)_random.NextDouble() * 2f - 1f) * SparkSpread;
                float speed = SparkSpeed * (0.6f + 0.8f * (float)_random.NextDouble());
                _live.Add(new Particle
                {
                    Position = from,
                    Velocity = new Vector2(-side * MathF.Cos(angle), MathF.Sin(angle)) * speed,
                    Life = SparkLife * (0.6f + 0.8f * (float)_random.NextDouble()),
                    Tint = Palette.GoldLight,
                });
            }
            // Side-on: tall and thin, half the booster ring.
            _rings.Add(new Ring
            {
                Centre = from,
                Width = RingHeight * SideRingScale * 1.4f,
                Height = RingWidth * SideRingScale,
            });
        }

        private void DrawParticles()
        {
            var canvas = CanvasServer.Current;
            canvas.Clear(_particles);
            foreach (Particle p in _live)
            {
                float t = p.Age / p.Life;
                Color color = new(p.Tint, 1f - t);
                float size = ParticleSize * (1f - 0.5f * t);
                canvas.AddRect(_particles,
                    new Rect2(p.Position.X - size * 0.5f, p.Position.Y - size * 0.5f, size, size), color);
            }

            // Ignition rings: an oval left where the booster was lit, growing and fading in place.
            foreach (Ring ring in _rings)
            {
                float t = ring.Age / RingLife;
                float grow = 1f + (RingGrow - 1f) * t;
                float rx = ring.Width * 0.5f * grow, ry = ring.Height * 0.5f * grow;
                Color color = new(Palette.GoldLight, 1f - t);
                Vector2 last = ring.Centre + new Vector2(rx, 0f);
                for (int k = 1; k <= RingSegments; k++)
                {
                    float a = k * MathF.Tau / RingSegments;
                    Vector2 next = ring.Centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
                    canvas.AddLine(_particles, last, next, color, 3f, true);
                    last = next;
                }
            }
        }

        private const float BeamSparkRate = 160f;  // / s while held
        private const int SweepSparksPerSwing = 40;
        private const int PulseSparkCount = 36;
        private float _beamSparkCarry;

        private float Rand(float min, float max) => min + (max - min) * (float)_random.NextDouble();

        private void Spark(Vector2 at, Vector2 velocity, float life, Color tint) =>
            _live.Add(new Particle { Position = at, Velocity = velocity, Life = life, Tint = tint });

        // Motes shed along the beam, drifting off it either way.
        private void BeamSparks(float dt)
        {
            _beamSparkCarry += BeamSparkRate * dt;
            Vector2 chest = Chest;
            while (_beamSparkCarry >= 1f)
            {
                _beamSparkCarry -= 1f;
                Vector2 at = chest + new Vector2(_facing * Rand(0f, BeamLength), Rand(-2f, 2f));
                Vector2 v = new(_facing * Rand(40f, 160f), Rand(-120f, 120f));
                Spark(at, v, Rand(0.15f, 0.35f), Rand(0f, 1f) < 0.5f ? Palette.Cyan : new Color(1f, 1f, 1f));
            }
        }

        // Thrown off the sweep's head, along the swing and a little outward.
        private void SweepSparks(float from, float to)
        {
            int count = (int)MathF.Round(SweepSparksPerSwing * (to - from) / SweepTime);
            Vector2 chest = Chest;
            for (int i = 0; i < count; i++)
            {
                float swing = Rand(from, to) / SweepTime;
                float a = -MathF.PI * 0.5f + MathF.PI * swing;
                Vector2 outward = new(_sweepSide * MathF.Cos(a), MathF.Sin(a));
                Vector2 along = new(_sweepSide * -MathF.Sin(a), MathF.Cos(a)); // direction of travel
                Vector2 v = along * Rand(250f, 550f) + outward * Rand(40f, 180f);
                Spark(chest + outward * SweepRadius, v, Rand(0.15f, 0.3f), Palette.TextBright);
            }
        }

        // A ring of sparks out from the body, riding just ahead of the pulse.
        private void PulseSparks()
        {
            Vector2 chest = Chest;
            for (int i = 0; i < PulseSparkCount; i++)
            {
                float a = (i + Rand(-0.3f, 0.3f)) * MathF.Tau / PulseSparkCount;
                Vector2 dir = new(MathF.Cos(a), MathF.Sin(a));
                Spark(chest + dir * 10f, dir * Rand(600f, 900f), Rand(0.3f, 0.45f), Palette.InfoSoft);
            }
        }

        private Vector2 Chest => _position + new Vector2(0f, -BodyHeight * 0.6f);

        private void DrawFx()
        {
            var canvas = CanvasServer.Current;
            canvas.Clear(_fx);
            Vector2 chest = Chest;

            if (_beam > 0f)
            {
                Vector2 end = chest + new Vector2(_facing * BeamLength, 0f);
                canvas.AddLine(_fx, chest, end, new Color(Palette.Cyan, 0.35f * _beam), BeamWidth * 2.2f, true);
                canvas.AddLine(_fx, chest, end, new Color(1f, 1f, 1f, _beam), BeamWidth * 0.5f, true);
            }

            if (_sweepAge >= 0f)
            {
                // Head swings from straight up (-90°) to straight down (+90°) on the facing side;
                // the arc behind it is the trail, fading once the swing is done.
                float swing = MathF.Min(_sweepAge / SweepTime, 1f);
                float fade = _sweepAge <= SweepTime ? 1f : 1f - (_sweepAge - SweepTime) / SweepFade;
                int lit = Math.Max(1, (int)(SweepSegments * swing));
                Vector2 Point(int k)
                {
                    float a = -MathF.PI * 0.5f + MathF.PI * k / SweepSegments;
                    return chest + new Vector2(_sweepSide * MathF.Cos(a), MathF.Sin(a)) * SweepRadius;
                }
                for (int k = 0; k < lit; k++)
                {
                    float along = (k + 1f) / lit; // brighter toward the head
                    Color color = new(Palette.TextBright, fade * (0.25f + 0.75f * along));
                    canvas.AddLine(_fx, Point(k), Point(k + 1), color, 4f + 6f * along, true);
                }
            }

            foreach (float age in _pulses)
            {
                float t = age / PulseLife;
                float radius = PulseRadius * (1f - (1f - t) * (1f - t)); // fast out, easing
                Color color = new(Palette.InfoSoft, 1f - t);
                Vector2 last = chest + new Vector2(radius, 0f);
                for (int k = 1; k <= PulseSegments; k++)
                {
                    float a = k * MathF.Tau / PulseSegments;
                    Vector2 next = chest + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                    canvas.AddLine(_fx, last, next, color, 4f * (1f - t) + 1f, true);
                    last = next;
                }
            }
        }

        public void Dispose() => CanvasServer.Current.Free(_back);
    }
}
