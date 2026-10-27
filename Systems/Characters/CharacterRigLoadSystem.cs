using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Keeps each play-mode player's rig in step with its <see cref="PlayerCharacter"/>:
/// loads the character's XAF package when the character starts or switches,
/// starts its idle clip, and drops the rig for characters without one.
/// </summary>
public sealed class CharacterRigLoadSystem(ICharacterRigFiles files)
    : QuerySystem<PlayerCharacter, PlayfieldMode>
{
    public const string IdleClip = "idle";

    protected override void OnUpdate()
    {
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;

            string? path = CharacterRigs.PathFor(entity.GetComponent<PlayerCharacter>().Kind);
            if (entity.TryGetComponent(out CharacterRigSource source) && source.Path == path)
                continue;

            entity.RemoveComponent<CharacterRig>();
            entity.RemoveComponent<CharacterMotion>();
            entity.RemoveTag<CharacterRigMissing>();
            entity.RemoveTag<CharacterRigVisible>();
            if (path is null)
            {
                entity.RemoveComponent<CharacterRigSource>();
                continue;
            }

            entity.AddComponent(new CharacterRigSource(path));
            XafPackage? package = files.Load(path);
            if (package is null)
            {
                entity.AddTag<CharacterRigMissing>();
                continue;
            }

            var runtime = new AnimatorRuntime(package);
            if (!runtime.Play(IdleClip, 0f) && package.Clips.Count > 0)
                runtime.Play(package.Clips[0].Id, 0f);
            entity.AddComponent(new CharacterRig(runtime));
        }
    }
}
