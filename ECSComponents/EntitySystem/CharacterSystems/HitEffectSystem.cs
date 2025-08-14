// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.ECSComponents.EntitySystem.CharacterSystems
{
    public class HitEffectSystem : QuerySystem<CharacterEcs>
    {
        private readonly IVisualsMaster visualsMaster = DiProvider.Get<IVisualsMaster>();
        private readonly IClock clock = DiProvider.Get<IClock>();

        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref CharacterEcs character, Entity _) =>
            {
                foreach (var variable in NoteTypeUtils.ACTION_SHAPES.Where(var
                             => Input.IsActionJustPressed(var.text)))
                {
                    if (clock.IsPaused) return;
                    setupHitVisuals(variable.noteType, character.Position );
                }
            }));
        }

        private void setupHitVisuals(NoteType type, Vector2 pos)
        {
           RenderRid rid = RenderRid.Create(visualsMaster.GameplayerLayerRid, 0.7f)
                .SetZIndex(2)
                .SetTransform(new Transform2D(0, pos))
                .SetModulate(type.NoteColor())
                .AddRect(new Vector2(100, 100))
                .SetMaterial(Materials.HITS.Get(HitShaderId.Default));

            InstanceShaders.SetHitTime(rid, (float)clock.PlaybackTimeSec);

            if (type == NoteType.C) RenderingServer.CanvasItemSetInstanceShaderParameter(rid, "y_rot", 35f);

            clock.Stopped += () => RenderingServer.FreeRid(rid);
        }
    }
}
