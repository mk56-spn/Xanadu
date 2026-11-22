using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Diagnostics;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Storage.Animation;

namespace Xanadu.Logic.Characters;

/// <summary>
/// Poses the stand-in bone puppet (<see cref="PackagePath"/>) from what the
/// body is doing: running on the floor, standing when stopped (at a wall), in the air, just dashed (an orb grab
/// is a dash), or hanging from an orb. Clips run on the body's song clock, so
/// they hold still with the song.
/// </summary>
public sealed class PuppetSystem : QuerySystem<CurveBody, PlayfieldMode>
{
    public const string PackagePath = "Assets/Characters/standin.xaf";

    /// <summary>How long the dash pose holds after a dash, ms.</summary>
    private const double DashPoseMs = 180;

    private XafPackage? _package;
    private bool _loaded;
    private AnimatorRuntime? _runtime;
    private double _lastMs = double.NaN;
    private bool _facingLeft;

    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.Entities)
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play || Package() is not { } package)
                continue;

            CurveBody body = entity.GetComponent<CurveBody>();
            Playfield playfield = entity.GetComponent<Playfield>();
            _runtime ??= new AnimatorRuntime(package);

            double now = body.SongMs;
            float delta = double.IsNaN(_lastMs) || now < _lastMs ? 0f : (float)((now - _lastMs) / 1000d);
            _lastMs = now;

            string clip = ClipFor(body, playfield);
            _runtime.Play(clip, clip == "dash" ? 0.04f : 0.12f);
            _runtime.Update(delta);

            if (body.Velocity.X < -1f)
                _facingLeft = true;
            else if (body.Velocity.X > 1f)
                _facingLeft = false;

            var puppet = new Puppet
            {
                Package = package,
                Bones = XafRig.WorldBones(package, _runtime.Pose),
                FacingLeft = _facingLeft,
                Clip = clip,
            };
            // Added once, after the loop; from then on written in place.
            if (entity.HasComponent<Puppet>())
                entity.GetComponent<Puppet>() = puppet;
            else
                CommandBuffer.AddComponent(entity.Id, puppet);
        }
    }

    private static string ClipFor(in CurveBody body, in Playfield playfield)
    {
        if (body.OnOrb)
            return "hang";
        double sinceDash = body.SongMs - body.DashStartMs;
        if (body.Dashing || (sinceDash >= 0 && sinceDash < DashPoseMs))
            return "dash";
        bool grounded = body.Position.Y >= PlayRules.FloorY(playfield) - PlayRules.BodyHeight / 2f - 1f && body.Velocity.Y >= 0f;
        if (!grounded)
            return "air";
        // Up against a wall the wall has taken the sideways speed: stand, don't run on the spot.
        return MathF.Abs(body.Velocity.X) < 1f ? "stand" : "run";
    }

    private XafPackage? Package()
    {
        if (_loaded)
            return _package;
        _loaded = true;
        try
        {
            _package = XafFiles.Load(PackagePath);
            if (_package is null)
                GameLog.Error($"Puppet rig missing: {PackagePath}");
        }
        catch (Exception exception) when (exception is XafFormatException or System.IO.IOException)
        {
            GameLog.Error($"Puppet rig unreadable: {exception.Message}");
        }

        return _package;
    }
}
