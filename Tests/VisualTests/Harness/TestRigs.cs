using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Characters;

namespace Xanadu.Tests.VisualTests;

public static partial class TestContent
{
    /// <summary>
    /// Dresses <paramref name="player"/> in the contact rig, idling. The harness's
    /// <c>res://</c> is its own folder, so the game's rig path misses; this loads
    /// the same package from the repo instead. The source keeps the game's path,
    /// so the load system leaves the rig alone.
    /// </summary>
    public static void DressInContactRig(Entity player)
    {
        if (player.HasComponent<CharacterRig>())
            return;

        string path = Xanadu.Storage.GamePaths.Globalize("res://Tools/Animator/Animations/Characters/contact.xaf");
        XafPackage? package = new DiskCharacterRigFiles().Load(path);
        if (package is null)
            return;

        var runtime = new AnimatorRuntime(package);
        runtime.Play(ContactClip.Idle.Id(), 0f);
        player.RemoveTag<CharacterRigMissing>();
        player.AddComponent(new CharacterRig(runtime));
    }
}
