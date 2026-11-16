using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Model.Settings;
using Xanadu.Systems.Physics;
using Xanadu.Ui.Ecs;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Playfields;

namespace Xanadu.Systems.Playfields;

/// <summary>The player stuck to a wall by a wall note, sliding down it (<see cref="WallSlideSystem"/>).</summary>
public struct WallSlide(float face, int side) : IComponent
{
    /// <summary>The wall's face, in screen x.</summary>
    public float Face = face;

    /// <summary>Which side of the player the wall is on: 1 right, -1 left.</summary>
    public int Side = side;

    /// <summary>Seconds since the last puff of wall particles.</summary>
    public float SinceParticles;
}

/// <summary>
/// Wall notes (<see cref="NoteKind.Wall"/>): a flat note set on a wall's face,
/// its hitbox sticking out a little from it. Touched within the judgement
/// window of its beat, it sticks the player to the wall: she slides down at
/// <see cref="Speed"/>, one speed for every wall, throwing wall particles, until
/// she lands, the wall ends, or a dash or momentum note takes her off it. Like
/// Contact barriers, wall notes take no press and aren't scored; one never
/// touched leaves the field once its window has passed.
/// </summary>
public sealed class WallSlideSystem(EntityStore entities, OffsetSettings? offsets = null)
    : QuerySystem<Player, PhysicsBody, Position, PlayfieldView, Playhead>
{
    /// <summary>How fast every wall slide goes down, in playfield units per second.</summary>
    public const float Speed = 260f;

    /// <summary>How far the hitbox sticks out from the wall either way, in playfield units.</summary>
    public const float Protrude = 14f;

    /// <summary>Seconds between puffs of wall particles while sliding.</summary>
    public const float ParticleSeconds = 0.07f;

    private readonly OffsetSettings _offsets = offsets ?? new OffsetSettings(persist: false);

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        long window = (long)ScoreRules.JudgementDeviation(Judgement.Miss);
        float delta = Tick.deltaTime;

        Query.ForEachEntity((ref player, ref body, ref position, ref view, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>() || IsWaiting(entity))
                return;

            Vector2 centre = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            if (entity.HasComponent<WallSlide>())
            {
                Slide(entity, ref entity.GetComponent<WallSlide>(), body, player.Size, view, centre, delta, buffer);
                return;
            }

            long judgedTime = playhead.TimeMs - PlayOffset.TotalMs(entity, _offsets);
            WallTouch touch = entities.Cached<Note>().EachEntity(
                new WallTouch(entities, view, buffer, centre, player.Size, judgedTime, window));
            if (touch.Id == 0)
                return;

            Stick(entity, body, player.Size, touch.Screen.X, centre, buffer);
            buffer.DeleteEntity(touch.Id);
        });
    }

    /// <summary>A wall note's hitbox in screen space: its slab, sticking out <see cref="Protrude"/> both ways.</summary>
    public static Rect2 HitBox(Vector2 noteScreen, PlayfieldView view)
    {
        Vector2 slab = PlayfieldMetrics.WallNoteSize(PlayfieldMetrics.NoteSize * view.Scale / 2);
        Vector2 size = slab + new Vector2(Protrude * 2 * view.Scale, 0);
        return new Rect2(noteScreen - size / 2, size);
    }

    private static bool Touches(Rect2 box, Vector2 centre, Vector2 size) =>
        box.Intersects(new Rect2(centre - size / 2, size));

    /// <summary>Sticks the player to the wall at <paramref name="face"/>, on the side the note is on.</summary>
    private void Stick(Entity player, PhysicsBody body, Vector2 size, float face, Vector2 centre, CommandBuffer buffer)
    {
        int side = face >= centre.X ? 1 : -1;
        DashHangSystem.End(player, body, buffer);
        GroundStepSystem.Cancel(player, buffer);
        buffer.AddComponent(player.Id, new WallSlide(face, side));

        Vector2 contact = new(face, centre.Y);
        entities.CreateEntity(new ParticleBurst(
            contact, GameParticles.Landing, PlayfieldColors.WallNoteColor, new Vector2(4f, size.Y), heading: new Vector2(-side, 0)));
        Pin(body, size, face, side, centre);
    }

    private void Slide(Entity player, ref WallSlide slide, PhysicsBody body, Vector2 size, PlayfieldView view, Vector2 centre, float delta, CommandBuffer buffer)
    {
        bool landed = player.TryGetComponent(out Playfield playfield) &&
                      PlayerGround.TryStanding(player, view, playfield, centre, out _);
        if (landed || !WallBeside(player.Store, view, slide.Face, centre, size))
        {
            buffer.RemoveComponent<WallSlide>(player.Id);
            return;
        }

        Pin(body, size, slide.Face, slide.Side, centre);
        body.Body.SetLinearVelocity(new Vector2(0f, Speed * view.Scale / Box2DPhysicsWorld.PixelsPerMeter));

        slide.SinceParticles += delta;
        if (slide.SinceParticles < ParticleSeconds)
            return;

        slide.SinceParticles = 0f;
        entities.CreateEntity(new ParticleBurst(
            new Vector2(slide.Face, centre.Y + size.Y / 2), GameParticles.Ember, PlayfieldColors.WallNoteColor, new Vector2(2f, 6f),
            heading: new Vector2(-slide.Side, -1f)));
    }

    /// <summary>Holds the player's side flush against the wall's face.</summary>
    private static void Pin(PhysicsBody body, Vector2 size, float face, int side, Vector2 centre)
    {
        var pinned = new Vector2(face - side * size.X / 2, centre.Y);
        if (pinned != centre)
            body.Body.SetTransform(Box2DPhysicsWorld.ToPhysics(pinned), body.Body.GetAngle());
    }

    /// <summary>Whether a block's side lies along <paramref name="face"/> beside the player's centre, so there is still wall to slide on.</summary>
    public static bool WallBeside(EntityStore store, PlayfieldView view, float face, Vector2 centre, Vector2 size)
    {
        return store.Cached<Block>().EachEntity(new BlockFace(store, view, face, centre, size.X / 2)).Found;
    }

    /// <summary>Takes the player off its wall, as a dash or momentum note does.</summary>
    public static void End(Entity player, CommandBuffer buffer)
    {
        if (player.HasComponent<WallSlide>())
            buffer.RemoveComponent<WallSlide>(player.Id);
    }

    private static bool IsWaiting(Entity entity) =>
        entity.TryGetComponent(out PlaySession session) && (!session.Started || session.Finished || session.Died);

    /// <summary>
    /// The first live wall note in the window whose hitbox the player touches.
    /// Wall notes whose window has passed are deleted on the way.
    /// </summary>
    private struct WallTouch(
        EntityStore store,
        PlayfieldView view,
        CommandBuffer buffer,
        Vector2 centre,
        Vector2 size,
        long judgedTime,
        long window) : IEachEntity<Note>
    {
        public int Id;
        public Vector2 Screen;

        public void Execute(ref Note note, int id)
        {
            if (Id != 0 || note.Kind != NoteKind.Wall)
                return;

            long early = note.TimeMs - judgedTime;
            if (early < -window)
            {
                buffer.DeleteEntity(id);
                return;
            }

            if (Math.Abs(early) > window || NoteFade.Alpha(note, judgedTime) <= 0)
                return;

            Vector2 noteScreen = view.PlayfieldToScreen(GroupOffset.Shown(store.GetEntityById(id), note.Position));
            if (!Touches(HitBox(noteScreen, view), centre, size))
                return;

            Id = id;
            Screen = noteScreen;
        }
    }

    /// <summary>Whether any flat block has a side along <paramref name="face"/> beside <paramref name="centre"/>.</summary>
    private struct BlockFace(EntityStore store, PlayfieldView view, float face, Vector2 centre, float slack) : IEachEntity<Block>
    {
        public bool Found;

        public void Execute(ref Block block, int id)
        {
            if (Found || block.Slope != BlockSlope.Off)
                return;

            Entity entity = store.GetEntityById(id);
            Vector2 topLeft = view.PlayfieldToScreen(GroupOffset.Shown(entity, block.Position));
            Vector2 bottomRight = view.PlayfieldToScreen(GroupOffset.Shown(entity, block.End));
            bool alongside = centre.Y >= topLeft.Y && centre.Y <= bottomRight.Y;
            bool onFace = Math.Abs(topLeft.X - face) <= slack || Math.Abs(bottomRight.X - face) <= slack;
            Found = alongside && onFace;
        }
    }
}
