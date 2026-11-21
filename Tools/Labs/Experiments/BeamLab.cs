using System;
using System.Collections.Generic;
using MoonWorks.Input;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A grey body under gravity over a floor. F / J dash left / right,
/// instantly, stopping at the first column in the way or the wall.
/// Short-lived columns are sprinkled around: a dash that meets one latches on
/// and climbs it at a fixed speed for as long as the dash key stays held. A
/// column you are climbing never expires under you. D / Space and K still hang
/// with an arc down / up.
/// </summary>
internal sealed class BeamLab : ILabExperiment
{
    public string Name => "HANG";

    public string[] Notes =>
    [
        "Grey body under gravity, on a floor.",
        "F / J: dash left / right, instantly,",
        "until a wall or a column.",
        "Floor-to-ceiling columns flicker.",
        "Dash onto one, keep the key held:",
        "you climb it at a fixed speed, up",
        "to the ceiling.",
        "Landing on one renews the rest.",
        "Leave it (let go, or dash off",
        "either way) and it's gone.",
        "D / Space: hang, arc down.",
        "K: hang, arc up.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : IToolStage
    {
        private const float BodyWidth = 40f;      // 1 × 2.5, like the play body
        private const float BodyHeight = 100f;

        private const float Gravity = 1800f;      // px / s²
        private const float MaxFall = 1100f;      // px / s

        private const float ClimbSpeed = 450f;    // px / s, up a column

        private const int ColumnCount = 3;        // kept alive on the stage
        private const float ColumnWidth = 8f;
        private const float ColumnLife = 2.6f;    // s, not counting time held on
        private const float ColumnFadeIn = 0.2f;
        private const float ColumnFadeOut = 0.4f;

        private const float ArcRadius = 150f;
        private const float ArcReach = 120f;
        private const float OpenTime = 0.5f;
        private const float FullSpan = MathF.PI;
        private const float ArcFade = 0.18f;
        private const int ArcSegments = 40;

        private const float FloorHeight = 24f;

        // Down, up: the hang arcs left on D / Space and K.
        private static readonly Vector2[] ArcDirections = [new(0f, 1f), new(0f, -1f)];
        private readonly Arc[] _arcs = new Arc[2];

        private readonly Handle _back;
        private readonly Handle _world;
        private readonly Handle _fx;
        private readonly Handle _body;
        private readonly Rect2 _area;
        private readonly Random _random = new(1);
        private readonly List<Particle> _live = [];
        private readonly List<Column> _columns = [];

        // Feet, in stage pixels.
        private float _x;
        private float _y;
        private float _vy;                        // px / s, + is down

        private float _trailFrom, _trailTo, _trailY; // the last dash's streak, world
        private float _trailAge = 1f;
        private const float TrailLife = 0.15f;
        private float _dashSide;
        private KeyCode _dashKey;
        private int _climbing = -1;               // column index, or -1

        private struct Column
        {
            public float X;                       // centre, world
            public float Age;
        }

        private struct Arc
        {
            public bool Held;
            public float Open;
            public float Fade;
        }

        private struct Particle
        {
            public Vector2 Position;              // world
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
            canvas.AddRect(_back, new Rect2(area.Position.X, area.Position.Y + area.Size.Y - FloorHeight, area.Size.X, FloorHeight), Palette.Muted);

            _world = canvas.CreateItem();
            canvas.SetParent(_world, _back);
            _fx = canvas.CreateItem();
            canvas.SetParent(_fx, _back);
            _body = canvas.CreateItem();
            canvas.SetParent(_body, _back);
            canvas.AddRect(_body, new Rect2(-BodyWidth * 0.5f, -BodyHeight, BodyWidth, BodyHeight), Palette.TextDim);

            _x = area.Position.X + area.Size.X * 0.5f;
            _y = Floor;
            for (int i = 0; i < ColumnCount; i++)
                _columns.Add(NewColumn(Rand(0f, ColumnLife)));
        }

        private float Left => _area.Position.X;
        private float Right => _area.Position.X + _area.Size.X;
        private float Top => _area.Position.Y;
        private float Bottom => _area.Position.Y + _area.Size.Y;
        private float Floor => Bottom - FloorHeight;
        private float Ceiling => Top + BodyHeight;
        private float Rand(float min, float max) => min + (max - min) * (float)_random.NextDouble();

        // Floor to ceiling, so only x matters: keep clear of the body and the others.
        private Column NewColumn(float age = 0f)
        {
            float x = 0f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                x = Rand(Left + 40f, Right - 40f);
                bool clear = MathF.Abs(x - _x) > MinSpacing;
                foreach (Column other in _columns)
                    if (!float.IsNaN(other.X))
                        clear &= MathF.Abs(x - other.X) > MinSpacing;
                if (clear)
                    break;
            }
            return new Column { X = x, Age = age };
        }

        private const float MinSpacing = 80f;

        private static float Alpha(in Column c) =>
            MathF.Min(MathF.Min(c.Age / ColumnFadeIn, 1f), MathF.Min((ColumnLife - c.Age) / ColumnFadeOut, 1f));

        public void Update(in ToolFrame frame)
        {
            Keyboard? keys = frame.Inputs?.Keyboard;
            bool Down(KeyCode key) => keys?.IsDown(key) ?? false;
            bool Pressed(KeyCode key) => keys?.IsPressed(key) ?? false;
            float step = MathF.Min(frame.Delta, 1f / 30f);

            // A fresh F / J press dashes, and drops any column we're on.
            if (Pressed(KeyCode.F)) StartDash(-1f, KeyCode.F);
            if (Pressed(KeyCode.J)) StartDash(1f, KeyCode.J);

            bool hanging = false;
            bool[] arcHeld = [Down(KeyCode.D) || Down(KeyCode.Space), Down(KeyCode.K)];
            for (int i = 0; i < 2; i++)
            {
                ref Arc arc = ref _arcs[i];
                if (arcHeld[i] && !arc.Held)
                    arc.Open = 0f;
                arc.Held = arcHeld[i];
                if (arcHeld[i])
                {
                    hanging = true;
                    arc.Open += step;
                    arc.Fade = 1f;
                }
                else
                    arc.Fade = MathF.Max(0f, arc.Fade - step / ArcFade);
            }

            if (_climbing >= 0)
                Climb(Down(_dashKey), step);
            else
            {
                _vy = hanging ? 0f : MathF.Min(_vy + Gravity * step, MaxFall);
                _y += _vy * step;
                if (_y >= Floor)
                {
                    _y = Floor;
                    _vy = 0f;
                }
            }

            _trailAge += step;
            AgeColumns(step);
            AgeParticles(step);

            CanvasServer.Current.SetTransform(_body, new Transform2D(0f, new Vector2(_x, _y)));
            Draw();
        }

        // Instant: we land on the first column that way, or the wall, this very frame.
        // Leaving a column (dashing off it included) deletes it; landing on one renews all the rest.
        private void StartDash(float side, KeyCode key)
        {
            if (_climbing >= 0)
                Leave();
            _dashSide = side;
            _dashKey = key;
            _vy = 0f;
            float from = _x;

            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _columns.Count; i++)
            {
                Column c = _columns[i];
                if (Alpha(c) <= 0.2f) continue;                     // too faint to grab
                float gap = (c.X - _x) * side;
                if (gap > 1f && gap < best)
                {
                    best = gap;
                    hit = i;
                }
            }

            if (hit >= 0)
            {
                _x = _columns[hit].X;
                _climbing = hit;
                for (int i = 0; i < _columns.Count; i++)
                    if (i != hit)
                        _columns[i] = new Column { X = float.NaN };     // clear first so spacing ignores them
                for (int i = 0; i < _columns.Count; i++)
                    if (i != hit)
                        _columns[i] = NewColumn();
                Burst(new Vector2(_x, _y - BodyHeight * 0.5f), -side, Palette.GoldLight, 22);
            }
            else
            {
                _x = side < 0f ? Left + BodyWidth * 0.5f : Right - BodyWidth * 0.5f;
                Burst(new Vector2(_x + side * BodyWidth * 0.5f, _y - BodyHeight * 0.5f), -side, Palette.TextBright, 14);
            }

            _trailFrom = from;
            _trailTo = _x;
            _trailY = _y;
            _trailAge = 0f;
        }

        private void Leave()
        {
            int was = _climbing;
            _climbing = -1;
            _columns[was] = new Column { X = float.NaN };
            _columns[was] = NewColumn();
        }

        // Fixed-speed rise for as long as the key that put us here is held; let go and we fall from rest.
        private void Climb(bool held, float step)
        {
            _vy = 0f;
            if (!held)
            {
                Leave();
                return;
            }
            _y = MathF.Max(_y - ClimbSpeed * step, Ceiling);
            if (_random.NextDouble() < 0.6)
            {
                float side = _random.NextDouble() < 0.5 ? -1f : 1f;
                _live.Add(new Particle
                {
                    Position = new Vector2(_x + side * BodyWidth * 0.5f, _y - Rand(0f, BodyHeight * 0.3f)),
                    Velocity = new Vector2(side * Rand(40f, 160f), Rand(60f, 220f)),
                    Life = Rand(0.2f, 0.35f),
                    Tint = Palette.GoldLight,
                });
            }
        }

        private void AgeColumns(float step)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (i == _climbing)
                    continue;                                       // on it: it waits for us
                Column c = _columns[i];
                c.Age += step;
                _columns[i] = c.Age >= ColumnLife ? NewColumn() : c;  // replaced in place, indices stay stable
            }
        }
        private void AgeParticles(float step)
        {
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
                _live[i] = p;
            }
        }

        private void Burst(Vector2 at, float side, Color tint, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, MathF.Tau);
                Vector2 v = new Vector2(MathF.Cos(a), MathF.Sin(a)) * Rand(80f, 320f) + new Vector2(side * Rand(100f, 400f), 0f);
                _live.Add(new Particle { Position = at, Velocity = v, Life = Rand(0.2f, 0.4f), Tint = tint });
            }
        }

        private (float Half, float Radius) Shape(in Arc arc)
        {
            float t = MathF.Min(arc.Open / OpenTime, 1f);
            float ease = 1f - (1f - t) * (1f - t);
            return (FullSpan * 0.5f * ease, ArcRadius + ArcReach * ease);
        }

        private void Draw()
        {
            var canvas = CanvasServer.Current;
            canvas.Clear(_world);

            for (int i = 0; i < _columns.Count; i++)
            {
                Column c = _columns[i];
                float a = i == _climbing ? 1f : Alpha(c);
                if (a <= 0f) continue;
                float y0 = Top, y1 = Floor;
                if (i == _climbing)
                    canvas.AddRect(_world, new Rect2(c.X - ColumnWidth * 1.5f, y0, ColumnWidth * 3f, y1 - y0), new Color(Palette.Gold, 0.25f));
                canvas.AddRect(_world, new Rect2(c.X - ColumnWidth * 0.5f, y0, ColumnWidth, y1 - y0),
                    new Color(i == _climbing ? Palette.GoldLight : Palette.Gold, a * (i == _climbing ? 1f : 0.75f)));
            }

            canvas.Clear(_fx);
            Vector2 chest = new(_x, _y - BodyHeight * 0.5f);
            for (int i = 0; i < 2; i++)
            {
                Arc arc = _arcs[i];
                if (arc.Fade <= 0f)
                    continue;
                (float half, float radius) = Shape(arc);
                float facing = MathF.Atan2(ArcDirections[i].Y, ArcDirections[i].X);
                int segments = Math.Max(2, (int)(ArcSegments * half / (FullSpan * 0.5f)));
                Vector2 Point(int k)
                {
                    float a = facing - half + 2f * half * k / segments;
                    return chest + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                }
                for (int k = 0; k < segments; k++)
                {
                    float mid = 1f - MathF.Abs((k + 0.5f) / segments * 2f - 1f);
                    float width = 3f + 9f * mid;
                    canvas.AddLine(_fx, Point(k), Point(k + 1), new Color(Palette.Cyan, 0.35f * arc.Fade), width * 2.2f, true);
                    canvas.AddLine(_fx, Point(k), Point(k + 1), new Color(1f, 1f, 1f, arc.Fade * (0.4f + 0.6f * mid)), width * 0.5f, true);
                }
            }

            foreach (Particle p in _live)
            {
                float t = p.Age / p.Life;
                float size = 6f * (1f - 0.5f * t);
                Vector2 at = p.Position;
                if (at.Y < Top || at.Y > Floor) continue;
                canvas.AddRect(_fx, new Rect2(at.X - size * 0.5f, at.Y - size * 0.5f, size, size), new Color(p.Tint, 1f - t));
            }

            if (_trailAge < TrailLife)
            {
                // The streak the instant dash leaves where we were.
                float t = _trailAge / TrailLife;
                float y = _trailY - BodyHeight * 0.5f;
                canvas.AddLine(_fx, new Vector2(_trailFrom, y), new Vector2(_trailTo, y),
                    new Color(Palette.TextDim, 0.5f * (1f - t)), BodyHeight * (0.8f - 0.6f * t), true);
            }
        }

        public void Dispose() => CanvasServer.Current.Free(_back);
    }
}
