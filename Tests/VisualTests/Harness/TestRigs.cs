using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Characters;

namespace Xanadu.Tests.VisualTests;

public static partial class TestContent
{
    /// <summary>
    /// Dresses <paramref name="player"/> in the contact rig, idling: the package
    /// the game loads, put on directly so the load system leaves the rig alone.
    /// </summary>
    public static void DressInContactRig(Entity player)
    {
        if (player.HasComponent<CharacterRig>())
            return;

        string path = Xanadu.Storage.GamePaths.Full("Tools/Animator/Animations/Characters/contact.xaf");
        XafPackage? package = new DiskCharacterRigFiles().Load(path);
        if (package is null)
            return;

        var runtime = new AnimatorRuntime(package);
        runtime.Play(ContactClip.Idle.Id(), 0f);
        player.AddComponent(new CharacterRig(runtime));
    }
}
