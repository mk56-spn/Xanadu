using System.Collections.Generic;
using MoonWorks.Input;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Floating orbs, each crossing at one set x speed while their height wanders
/// freely (the shape of the hold). F / J dash instantly to the first orb that
/// way (or the wall); hold the key to ride it. Letting go leaves at the orb's
/// x speed and with whatever vertical velocity it had at that instant, and
/// gravity takes it from there, with floor fans blowing you back up. No drag, no bounces.
/// </summary>
internal sealed class OrbLab : ILabExperiment
{
    public string Name => "ORBS";

    public string[] Notes =>
    [
        "Orbs cross at one set x speed;",
        "their height does as it pleases.",
        "F / J: dash instantly to the first",
        "orb that way (or the wall).",
        "Hold the key: hang from the orb.",
        "Let go: you leave at the orb's x",
        "speed and its vertical motion at",
        "that instant, and keep both.",
        "Then gravity takes over, and floor",
        "fans blow you back up. No drag;",
        "walls stop you, nothing bounces.",
        "Faint line: where each orb goes next.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : IToolStage
    {
        private const float BodyWidth = 40f;      // 1 × 2.5, like the play body
        private const float BodyHeight = 100f;
        private const float FloorHeight = 24f;
        private const float Gravity = 1800f;      // px / s², off an orb only

        private const int OrbCount = 4;
        private const float OrbRadius = 16f;
        private const float OrbSpeedX = 600f;     // px / s, every orb, every time
        private const float Reach = BodyHeight * 0.5f + OrbRadius; // vertical slack a dash forgives
        private const float PreviewTime = 1.2f;   // s of path drawn ahead of each orb
        private const int PreviewSteps = 40;

        private const float TrailTime = 1.5f;     // s of body trail
        private const int TrailMax = 240;

        private readonly Handle _back;
        private readonly Handle _fx;
        private readonly Rect2 _area;
        private readonly Random _random = new(3);
        private readonly List<Orb> _orbs = [];
        private readonly List<Particle> _live = [];
        private readonly List<(Vector2 At, float Age)> _trail = [];

        private Vector2 _centre;                  // body centre, stage pixels
        private Vector2 _velocity;
        private int _riding = -1;                 // orb index, or -1
        private KeyCode _rideKey;
        private float _time;

        // Look only: the figure swings round its hand like a pendulum on the orb, and settles upright off it.
        private const float SwingLength = 60f;    // px from hand to the figure's weight
        private const float SwingDamping = 2.5f;  // 1 / s while hanging
        private const float SwingSettle = 60f;    // spring back upright off an orb, 1 / s²
        private const float SwingSettleDamping = 9f;
        private const float SwingMax = 1.2f;      // rad either way
        private float _swing, _swingSpeed, _face = 1f;
        private Vector2 _hangVelocity;

        private static readonly Vector2 Hang = new(0f, BodyHeight * 0.5f); // hand at the top of the body

        private float _dashFrom = float.NaN, _dashTo, _dashY, _dashAge = 1f;
        private const float DashLife = 0.15f;

        /// <summary>
        /// Height is a sum of two sines on the orb's own clock: smooth, free, and
        /// differentiable so the exit velocity is exact.
        /// </summary>
        private struct Orb
        {
            public float X;
            public float Direction;               // ±1
            public float Clock;
            public float Base, A1, W1, P1, A2, W2, P2;

            public readonly float Y(float clock) =>
                Base + A1 * MathF.Sin(W1 * clock + P1) + A2 * MathF.Sin(W2 * clock + P2);

            public readonly float YSpeed(float clock) =>
                A1 * W1 * MathF.Cos(W1 * clock + P1) + A2 * W2 * MathF.Cos(W2 * clock + P2);

            public readonly Vector2 At => new(X, Y(Clock));
        }

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

            // Start mid-air already moving: there is no standing still here.
            _centre = new(area.Position.X + area.Size.X * 0.5f, area.Position.Y + area.Size.Y * 0.5f);
            _velocity = new(OrbSpeedX, 0f);
            for (int i = 0; i < OrbCount; i++)
                _orbs.Add(NewOrb(Rand(Left + 60f, Right - 60f)));
        }

        private float Left => _area.Position.X;
        private float Right => _area.Position.X + _area.Size.X;
        private float Top => _area.Position.Y;
        private float Floor => _area.Position.Y + _area.Size.Y - FloorHeight;

        private float Rand(float min, float max) => min + (max - min) * (float)_random.NextDouble();

        // A fresh path; heights kept inside the stage with room for the body.
        private Orb NewOrb(float x)
        {
            float lo = Top + OrbRadius + 10f, hi = Floor - BodyHeight - 10f; // the body hangs below
            float span = (hi - lo) * 0.5f;
            float a1 = Rand(0.25f, 0.6f) * span, a2 = Rand(0f, 0.3f) * span;
            return new Orb
            {
                X = x,
                Direction = Rand(0f, 1f) < 0.5f ? -1f : 1f,
                Base = Rand(lo + a1 + a2, hi - a1 - a2),
                A1 = a1, W1 = Rand(0.8f, 2.2f), P1 = Rand(0f, MathF.Tau),
                A2 = a2, W2 = Rand(2.5f, 5f), P2 = Rand(0f, MathF.Tau),
            };
        }

        public void Update(in ToolFrame frame)
        {
            Keyboard? keys = frame.Inputs?.Keyboard;
            float step = MathF.Min(frame.Delta, 1f / 30f);
            _time += step;

            MoveOrbs(step);

            if (keys?.IsPressed(KeyCode.F) ?? false) Dash(-1f, KeyCode.F);
            if (keys?.IsPressed(KeyCode.J) ?? false) Dash(1f, KeyCode.J);

            if (_riding >= 0)
            {
                Orb orb = _orbs[_riding];
                if (keys?.IsDown(_rideKey) ?? false)
                {
                    _centre = orb.At + Hang;
                    _velocity = new(orb.Direction * OrbSpeedX, orb.YSpeed(orb.Clock));
                }
                else
                    Leave();
            }
            else
                Coast(step);

            Swing(step);

            _trail.Add((_centre, 0f));
            for (int i = _trail.Count - 1; i >= 0; i--)
            {
                var (at, age) = _trail[i];
                if (age + step >= TrailTime || _trail.Count > TrailMax && i == 0)
                    _trail.RemoveAt(i);
                else
                    _trail[i] = (at, age + step);
            }

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Particle p = _live[i];
                p.Age += step;
                if (p.Age >= p.Life)
                    _live.RemoveAt(i);
                else
                {
                    p.Position += p.Velocity * step;
                    _live[i] = p;
                }
            }
            _dashAge += step;

            Draw();
        }

        // Orbs bounce off the side walls, so a ride can go on as long as the hold does.
        private void MoveOrbs(float step)
        {
            for (int i = 0; i < _orbs.Count; i++)
            {
                Orb o = _orbs[i];
                o.Clock += step;
                o.X += o.Direction * OrbSpeedX * step;
                float lo = Left + BodyWidth * 0.5f, hi = Right - BodyWidth * 0.5f;
                if (o.X < lo || o.X > hi)
                {
                    o.X = Math.Clamp(o.X, lo, hi);
                    o.Direction = -o.Direction;
                }
                _orbs[i] = o;
            }
        }

        // Free flight from the exit velocity under gravity, lifted by the floor fans.
        // Nothing bounces: walls stop sideways motion, the floor and ceiling stop vertical.
        private void Coast(float step)
        {
            _velocity.Y += (Gravity - FanLift()) * step;
            _velocity.Y = MathF.Max(_velocity.Y, -FanMaxRise);
            _centre += _velocity * step;
            float lo = Left + BodyWidth * 0.5f, hi = Right - BodyWidth * 0.5f;
            float top = Top + BodyHeight * 0.5f, bottom = Floor - BodyHeight * 0.5f;
            if (_centre.X < lo || _centre.X > hi)
            {
                _centre.X = Math.Clamp(_centre.X, lo, hi);
                _velocity.X = 0f;
            }
            if (_centre.Y < top || _centre.Y > bottom)
            {
                _centre.Y = Math.Clamp(_centre.Y, top, bottom);
                _velocity.Y = 0f;
            }
        }

        // Fans tile the floor with gaps narrower than the body, so it always sits over one.
        // Lift is strongest at the floor and gone at the top of the draught.
        private const int FanCount = 4;
        private const float FanGap = 24f;
        private const float FanReach = 380f;      // px of draught above the floor
        private const float FanPush = 4200f;      // px / s² at the floor; gravity wins near the top
        private const float FanMaxRise = 700f;    // px / s

        private Rect2 Fan(int i)
        {
            float width = (_area.Size.X - FanGap * (FanCount - 1)) / FanCount;
            return new Rect2(Left + i * (width + FanGap), Floor - FanReach, width, FanReach);
        }

        private float FanLift()
        {
            float feet = _centre.Y + BodyHeight * 0.5f;
            float height = Floor - feet;
            if (height >= FanReach)
                return 0f;
            float l = _centre.X - BodyWidth * 0.5f, r = _centre.X + BodyWidth * 0.5f;
            for (int i = 0; i < FanCount; i++)
            {
                Rect2 fan = Fan(i);
                if (r > fan.Position.X && l < fan.Position.X + fan.Size.X)
                    return FanPush * (1f - height / FanReach);
            }
            return 0f;
        }

        // Instant, straight across: the nearest orb that way within reach of the body's height, else the wall.
        private void Dash(float side, KeyCode key)
        {
            if (_riding >= 0)
                Leave();
            float from = _centre.X;

            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _orbs.Count; i++)
            {
                Vector2 at = _orbs[i].At;
                float gap = (at.X - _centre.X) * side;
                if (gap > 1f && gap < best && MathF.Abs(at.Y - _centre.Y) <= Reach)
                {
                    best = gap;
                    hit = i;
                }
            }

            if (hit >= 0)
            {
                _riding = hit;
                _rideKey = key;
                _centre = _orbs[hit].At + Hang;
                Burst(_orbs[hit].At, Palette.GoldLight, 24);
            }
            else
            {
                _centre.X = side < 0f ? Left + BodyWidth * 0.5f : Right - BodyWidth * 0.5f;
                _velocity.X = 0f;
                Burst(_centre + new Vector2(side * BodyWidth * 0.5f, 0f), Palette.TextBright, 14);
            }

            _dashFrom = from;
            _dashTo = _centre.X;
            _dashY = _centre.Y;
            _dashAge = 0f;
        }

        // The velocity was already the orb's at this instant; the orb itself starts a new path elsewhere.
        private void Leave()
        {
            Orb was = _orbs[_riding];
            _riding = -1;
            Burst(was.At, Palette.Cyan, 16);
            _orbs[_orbs.IndexOf(was)] = NewOrb(Rand(Left + 60f, Right - 60f));
        }

        // Hanging: a pendulum driven by the hand's acceleration (the orb's wander, the catch, the wall turn).
        // Off an orb: a spring back upright. Look only; the body box never turns.
        private void Swing(float step)
        {
            if (MathF.Abs(_velocity.X) > 1f)
                _face = MathF.Sign(_velocity.X);
            if (_riding >= 0)
            {
                Vector2 a = (_velocity - _hangVelocity) / step;
                float pull = a.X * MathF.Cos(_swing) - (Gravity - a.Y) * MathF.Sin(_swing);
                _swingSpeed += (pull / SwingLength - SwingDamping * _swingSpeed) * step;
            }
            else
                _swingSpeed += (-SwingSettle * _swing - SwingSettleDamping * _swingSpeed) * step;
            _hangVelocity = _velocity;
            _swing += _swingSpeed * step;
            if (MathF.Abs(_swing) > SwingMax)
            {
                _swing = MathF.Sign(_swing) * SwingMax;
                _swingSpeed = 0f;
            }
        }

        private void DrawFigure()
        {
            var canvas = CanvasServer.Current;
            Vector2 hand = _centre - Hang;
            float c = MathF.Cos(_swing), s = MathF.Sin(_swing), f = _face;
            Vector2 P(float x, float y) => hand + new Vector2(x * f * c - y * s, x * f * s + y * c);
            void Limb(Vector2 a, Vector2 b) => canvas.AddLine(_fx, a, b, Palette.TextBright, 6f, true);

            Vector2 shoulder = P(0f, 36f), hip = P(-2f, 70f);
            Limb(P(0f, 0f), shoulder);                                    // gripping arm
            Limb(shoulder, hip);                                          // torso
            Limb(shoulder, P(8f, 50f)); Limb(P(8f, 50f), P(5f, 64f));     // free arm
            Limb(hip, P(-6f, 86f)); Limb(P(-6f, 86f), P(-12f, 100f));     // trailing leg
            Limb(hip, P(2f, 85f)); Limb(P(2f, 85f), P(-2f, 100f));        // other leg
            canvas.AddCircle(_fx, P(9f, 26f), 10f, Palette.TextBright);   // head, beside the arm
            canvas.AddCircle(_fx, hand, 5f, Palette.TextBright);
        }

        private void Burst(Vector2 at, Color tint, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, MathF.Tau);
                _live.Add(new Particle
                {
                    Position = at,
                    Velocity = new Vector2(MathF.Cos(a), MathF.Sin(a)) * Rand(80f, 340f),
                    Life = Rand(0.2f, 0.4f),
                    Tint = tint,
                });
            }
        }

        private void Draw()
        {
            var canvas = CanvasServer.Current;
            canvas.Clear(_fx);

            // Fans: a slab on the floor and a draught fading upward, with rising wisps.
            for (int i = 0; i < FanCount; i++)
            {
                Rect2 fan = Fan(i);
                const int bands = 8;
                for (int b = 0; b < bands; b++)
                {
                    float h = FanReach / bands;
                    canvas.AddRect(_fx, new Rect2(fan.Position.X, Floor - (b + 1) * h, fan.Size.X, h),
                        new Color(Palette.Info, 0.07f * (1f - (float)b / bands)));
                }
                canvas.AddRect(_fx, new Rect2(fan.Position.X, Floor - 6f, fan.Size.X, 6f), Palette.Info);
                for (int k = 0; k < 6; k++)
                {
                    float phase = (_time * 1.6f + k / 6f + i * 0.37f) % 1f;
                    float x = fan.Position.X + fan.Size.X * ((k * 0.618f + i * 0.31f) % 1f);
                    float y = Floor - phase * FanReach;
                    canvas.AddLine(_fx, new Vector2(x, y), new Vector2(x, y - 26f), new Color(Palette.Info, 0.4f * (1f - phase)), 2f, true);
                }
            }

            // Each orb's path for the next moment, with the wall bounces it will take.
            for (int i = 0; i < _orbs.Count; i++)
            {
                Orb o = _orbs[i];
                float x = o.X, dir = o.Direction, dt = PreviewTime / PreviewSteps;
                float lo = Left + BodyWidth * 0.5f, hi = Right - BodyWidth * 0.5f;
                Vector2 last = o.At;
                for (int k = 1; k <= PreviewSteps; k++)
                {
                    x += dir * OrbSpeedX * dt;
                    if (x < lo || x > hi)
                    {
                        x = Math.Clamp(x, lo, hi);
                        dir = -dir;
                    }
                    Vector2 next = new(x, o.Y(o.Clock + k * dt));
                    float fade = 1f - (float)k / PreviewSteps;
                    Color tint = i == _riding ? Palette.Gold : Palette.Cyan;
                    canvas.AddLine(_fx, last, next, new Color(tint, 0.35f * fade), 3f, true);
                    last = next;
                }
            }

            // The body's recent path: the shape of what you held.
            for (int i = 1; i < _trail.Count; i++)
            {
                float t = _trail[i].Age / TrailTime;
                canvas.AddLine(_fx, _trail[i - 1].At, _trail[i].At, new Color(Palette.TextDim, 0.5f * (1f - t)), 4f, true);
            }

            if (_dashAge < DashLife && !float.IsNaN(_dashFrom))
            {
                float t = _dashAge / DashLife;
                canvas.AddLine(_fx, new Vector2(_dashFrom, _dashY), new Vector2(_dashTo, _dashY),
                    new Color(Palette.TextDim, 0.5f * (1f - t)), BodyHeight * (0.8f - 0.6f * t), true);
            }

            for (int i = 0; i < _orbs.Count; i++)
            {
                Vector2 at = _orbs[i].At;
                bool riding = i == _riding;
                float r = OrbRadius * (riding ? 1.3f : 1f);
                canvas.AddCircle(_fx, at, r * 1.8f, new Color(riding ? Palette.Gold : Palette.Cyan, 0.2f));
                canvas.AddCircle(_fx, at, r, riding ? Palette.GoldLight : Palette.Cyan);
            }

            DrawFigure();

            foreach (Particle p in _live)
            {
                float t = p.Age / p.Life;
                float size = 6f * (1f - 0.5f * t);
                canvas.AddRect(_fx, new Rect2(p.Position.X - size * 0.5f, p.Position.Y - size * 0.5f, size, size), new Color(p.Tint, 1f - t));
            }
        }

        public void Dispose() => CanvasServer.Current.Free(_back);
    }
}
