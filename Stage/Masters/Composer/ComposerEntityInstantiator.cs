// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.Stage.Masters.Composer
{
    public static class ComposerEntityInstantiator
    {
        public static void RequestAddElement(IComposer composer)
        {
            Entity ent = composer.EntityStore.CreateEntity();

            // 1. Copy the base properties from the selected template (note or block).
            composer.SelectedTemplateEntity.CopyEntity(ent);

            // 2. If it's a note and a direction is selected, add the direction component.
            if (ent.TryGetComponent(out NoteEcs _))
            {
                if (!ent.HasComponent<HoldEcs>())
                {
                    if ( composer.SelectedDirection.HasValue)
                        ent.AddComponent(new DirectionEcs(composer.SelectedDirection.Value));
                }

                ent.AddComponent(new NoteEcs(composer.SelectedNoteType));

            }

            // 3. If it's a block and a shader is selected, add the shader component.
            if (ent.TryGetComponent(out BlockEcs _) && composer.SelectedBlockShaderId.HasValue)
            {
                ent.AddComponent(new MaterialEcs{ Shader =  composer.SelectedBlockShaderId.Value});
            }


            ent.AddTag<UnInitialized>();
            ent.AddTag<SelectionFlag>();

            Vector2 size = new(32, 32);
            if (ent.TryGetComponent(out RectEcs rect)) size = rect.Extents;

            ent.AddComponent(new ElementEcs { Transform = Transform2D.Identity with { Origin = position(size, composer.MousePosLocal) } });
        }
        private static Vector2 position(Vector2 size, Vector2 mousePos)
        {
            return (mousePos + size / 2).Snapped(size) - size / 2;
        }
    }
}
