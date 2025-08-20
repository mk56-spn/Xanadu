// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Animation;
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

        public HitEffectSystem()
        {
            clock.Stopped += () => {
                foreach (var canvases in effectsRef)
                    RenderingServer.FreeRid(canvases);
                effectsRef.Clear();
            };
        }
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

        private readonly AnimationMaterial animationMaterial = new();


        private readonly List<RenderRid> effectsRef = new(300);
        private void setupHitVisuals(NoteType type, Vector2 pos)
        {
            const float duration = 1f;

            RenderRid rid = setupRid()
                .AddRect(new Vector2(100, 100))
                .SetMaterial(Materials.HITS.Get(HitShaderId.Default));

            RenderRid line = setupRid()
                .AddRect(new Vector2(10, 3000))
                .SetMaterial(animationMaterial.GetRid());

           animationMaterial.SetAnimationDuration(line,duration);
           animationMaterial.SetAnimationStartCurrent(line);
           animationMaterial.SetScaled(line,Vector2.Down);

           effectsRef.Add(rid);
           effectsRef.Add(line);

           InstanceShaders.SetHitTime(rid, (float)clock.PlaybackTimeSec);
           if (type == NoteType.C) RenderingServer.CanvasItemSetInstanceShaderParameter(rid, "y_rot", 35f);
           clock.Stopped += () => RenderingServer.FreeRid(rid);


           RenderRid setupRid()=>
                RenderRid.Create(visualsMaster.GameplayerLayerRid, duration)
                   .SetZIndex(100)
                   .SetTransform(new Transform2D(0, pos))
                   .SetModulate(type.NoteColor());
        }
    }
}
