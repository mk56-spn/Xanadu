using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Projects Armed's equipped weapon into a play HUD label.</summary>
public sealed class WeaponHudSystem : QuerySystem<PlayerCharacter, WeaponHud>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref character, ref hud, entity) =>
        {
            string value = character.Kind == CharacterKind.Armed &&
                           entity.TryGetComponent(out ArmedWeapon weapon)
                ? weapon.Kind == WeaponKind.Spear ? "SPEAR" : "AXE"
                : "";

            if (hud.Label.IsNull || !hud.Label.HasComponent<Text>())
                return;

            ref Text text = ref hud.Label.GetComponent<Text>();
            if (text.Value == value)
                return;

            text.Value = value;
            buffer.AddTag<Dirty>(hud.Label.Id);
        });
    }
}
