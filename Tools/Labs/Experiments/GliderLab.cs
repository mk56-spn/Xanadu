using Xanadu.Core.Theme;
using Xanadu.Host.Tools;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A blue body with a triangle glider above it, sinking at a steady rate while
/// fans on either side push it sideways: each fan pushes harder the closer the
/// glider is, so it swings back and forth between them. Sideways speed is
/// capped at 1.5× the old glide's sideways speed, and the glide starts at that cap.
/// </summary>
internal sealed class GliderLab : ILabExperiment
{
    public string Name => "GLIDER";

    public string[] Notes =>
    [
        "Blue body, triangle glider above.",
        "Sinks steadily; fans on both sides",
        "push it away, harder when closer.",
        "Sideways speed capped at 1.5x the",
        "old glide; starts at the cap.",
        "Wraps to the top when it leaves.",
        "Yellow notes sit at each turn's",
        "apex: press Space as you reach one.",
        "Teal = hit, grey = missed.",
        "Kite's top corner points the way",
        "it's heading; it pitches nose-up",
        "as a fan bleeds off its speed.",
    ];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(parent, area);

    private sealed class Stage : IToolStage
    {
        // The old glide: 260 px/s along a 3 across : 1 down line.
        private const float OldSpeed = 260f;
        private const float BlocksAcrossPerDown = 3f;
        private static readonly float OldAcross = OldSpeed * BlocksAcrossPerDown / MathF.Sqrt(BlocksAcrossPerDown * BlocksAcrossPerDown + 1f);
        private static readonly float Sink = OldSpeed / MathF.Sqrt(BlocksAcrossPerDown * BlocksAcrossPerDown + 1f);
        private static readonly float MaxAcross = OldAcross * 1.5f;

        // Fan push: Strength at the fan's face, falling off (squared) to nothing at Reach.
        // Tuned so a glider at the cap turns round ~120 px from the fan (wing tip ~50 px off).
        private const float FanStrength = 1500f;  // px / s²
        private const float FanReach = 400f;      // px
        private const float FanWidth = 24f;

        private const float BodyWidth = 40f;      // 1 × 2.5, like the play body
        private const float BodyHeight = 100f;
        private const float WingSpan = 140f;
        private const float WingHeight = 36f;
        private const float WingGap = 10f;

        private readonly Handle _back;
        private readonly Handle _glider;
        private readonly Rect2 _area;
        private const float Step = 1f / 240f;     // fixed physics step, so the live glide matches the pre-run
        private const float HitWindow = 0.1f;     // ± seconds
        private const float NoteWidth = 80f;
        private const float NoteHeight = 20f;

        private readonly Handle _notes;
        private readonly System.Collections.Generic.List<Note> _targets = [];
        private const float KiteLean = 0.35f;     // top corner offset, × wing span
        private const float MaxTilt = 35f * MathF.PI / 180f;
        private const float PoseRate = 10f;       // 1/s, how fast heading and tilt catch up

        private Vector2 _position;
        private float _across;
        private float _heading = 1f;              // -1 left … 1 right
        private float _tilt;
        private float _time;
        private float _carry;

        private enum NoteState { Waiting, Hit, Missed }

        private sealed class Note(float time, Vector2 at)
        {
            public float Time = time;
            public Vector2 At = at;
            public NoteState State;
        }

        public Stage(Handle parent, Rect2 area)
        {
            _area = area;
            var canvas = CanvasServer.Current;
            _back = canvas.CreateItem();
            canvas.SetParent(_back, parent);
            canvas.AddRect(_back, area, Palette.Stage);
            DrawFans();

            _notes = canvas.CreateItem();
            canvas.SetParent(_notes, _back);

            _glider = canvas.CreateItem();
            canvas.SetParent(_glider, _back);
            DrawGlider();
            FindApexes();
            Restart();
        }

        private float Left => _area.Position.X + FanWidth;
        private float Right => _area.Position.X + _area.Size.X - FanWidth;

        private void DrawFans()
        {
            var canvas = CanvasServer.Current;
            float top = _area.Position.Y, height = _area.Size.Y;
            canvas.AddRect(_back, new Rect2(_area.Position.X, top, FanWidth, height), Palette.Muted);
            canvas.AddRect(_back, new Rect2(Right, top, FanWidth, height), Palette.Muted);

            // Short air streaks pointing inward from each fan.
            for (float y = top + 60f; y < top + height; y += 120f)
            {
                canvas.AddLine(_back, new(Left + 8f, y), new(Left + 48f, y), Palette.TextDim, 2f, true);
                canvas.AddLine(_back, new(Right - 8f, y), new(Right - 48f, y), Palette.TextDim, 2f, true);
            }
        }

        // Drawn around the body's centre; the item transform moves and tilts it.
        // The kite's top corner sits off to the side the glider is heading.
        private void DrawGlider()
        {
            var canvas = CanvasServer.Current;
            canvas.Clear(_glider);
            canvas.AddRect(_glider,
                new Rect2(-BodyWidth * 0.5f, -BodyHeight * 0.5f, BodyWidth, BodyHeight),
                Palette.PrimarySoft);

            float baseY = -BodyHeight * 0.5f - WingGap;
            Vector2[] wing =
            [
                new(-WingSpan * 0.5f, baseY),
                new(WingSpan * 0.5f, baseY),
                new(_heading * WingSpan * KiteLean, baseY - WingHeight),
            ];
            canvas.AddPolygon(_glider, wing, [Palette.TextBright, Palette.TextBright, Palette.TextBright]);
        }

        private void Restart()
        {
            _position = new(Left + WingSpan, _area.Position.Y + BodyHeight * 0.5f + WingGap + WingHeight + 20f);
            _across = MaxAcross;
            _time = 0f;
            _carry = 0f;
            _heading = 1f;
            _tilt = 0f;
            DrawGlider();
            foreach (Note note in _targets)
                note.State = NoteState.Waiting;
            DrawNotes();
            Place();
        }

        private bool Fallen => _position.Y - BodyHeight > _area.Position.Y + _area.Size.Y;

        // Runs the whole fall once ahead of time; a note goes wherever the
        // sideways speed flips sign (the apex of a turn round).
        private void FindApexes()
        {
            Restart();
            _targets.Clear();
            while (!Fallen)
            {
                float before = _across;
                Advance();
                if (MathF.Sign(before) != MathF.Sign(_across) && _across != 0f)
                    _targets.Add(new Note(_time, _position));
            }
        }

        private void Advance()
        {
            float push = Push(_position.X - Left) - Push(Right - _position.X);
            _across = Math.Clamp(_across + push * Step, -MaxAcross, MaxAcross);
            _position += new Vector2(_across, Sink) * Step;
            _time += Step;
        }

        private void DrawNotes()
        {
            var canvas = CanvasServer.Current;
            canvas.Clear(_notes);
            foreach (Note note in _targets)
            {
                Color color = note.State switch
                {
                    NoteState.Hit => Palette.Selected,
                    NoteState.Missed => Palette.Muted,
                    _ => Palette.Accent,
                };
                canvas.AddRect(_notes,
                    new Rect2(note.At.X - NoteWidth * 0.5f, note.At.Y - NoteHeight * 0.5f, NoteWidth, NoteHeight),
                    color);
            }
        }

        private void Place() =>
            CanvasServer.Current.SetTransform(_glider, new Transform2D(_tilt, _position));

        // Heading eases toward the side of travel; tilt pitches the nose up
        // while a fan is eating the sideways speed, and eases back otherwise.
        private void Pose(float dt)
        {
            float side = MathF.Sign(_across);
            float push = Push(_position.X - Left) - Push(Right - _position.X);
            bool braking = side != 0f && MathF.Sign(push) == -side;
            float lost = 1f - MathF.Abs(_across) / MaxAcross;
            float tiltTarget = braking ? -side * MaxTilt * lost : 0f;

            float ease = 1f - MathF.Exp(-PoseRate * dt);
            _heading += (side - _heading) * ease;
            _tilt += (tiltTarget - _tilt) * ease;
            DrawGlider();
        }

        private float Push(float distance)
        {
            float t = 1f - Math.Clamp(distance / FanReach, 0f, 1f);
            return FanStrength * t * t;
        }

        public void Update(in ToolFrame frame)
        {
            _carry += frame.Delta;
            while (_carry >= Step)
            {
                _carry -= Step;
                Advance();
            }

            bool changed = false;
            bool pressed = frame.Inputs?.Keyboard.IsPressed(MoonWorks.Input.KeyCode.Space) ?? false;
            foreach (Note note in _targets)
            {
                if (note.State != NoteState.Waiting)
                    continue;
                if (pressed && MathF.Abs(_time - note.Time) <= HitWindow)
                {
                    note.State = NoteState.Hit;
                    pressed = false;
                    changed = true;
                }
                else if (_time - note.Time > HitWindow)
                {
                    note.State = NoteState.Missed;
                    changed = true;
                }
            }
            if (changed)
                DrawNotes();

            if (Fallen)
                Restart();
            else
            {
                Pose(frame.Delta);
                Place();
            }
        }

        public void Dispose() => CanvasServer.Current.Free(_back);
    }
}
