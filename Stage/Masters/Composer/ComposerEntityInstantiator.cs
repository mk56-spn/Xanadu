// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Composer
{
    public static class ComposerEntityInstantiator
    {
        private static IClock iClock => DiProvider.Get<IClock>();
        public static void RequestAddElement(IComposer composer)
        {
            Entity ent = composer.EntityStore.CreateEntity();

            composer.SelectedTemplateEntity.CopyEntity(ent);

            if (ent.TryGetComponent(out NoteEcs note))
            {
                if (!ent.HasComponent<HoldEcs>())
                {
                    if ( composer.SelectedDirection.HasValue)
                        ent.AddComponent(new DirectionEcs(composer.SelectedDirection.Value));
                }

                if (ent.HasComponent<HoldEcs>())
                {
                    note.NoteType = NoteType.Main;
                }

                ent.AddComponent(new NoteEcs(composer.SelectedNoteType)
                {
                    TimingPoint =iClock.SnappedPlayBackTime()
                });
            }

            if (ent.TryGetComponent(out BlockEcs _) && composer.SelectedBlockShaderId.HasValue)
            {
                ent.AddComponent(new BlockMaterialEcs{ Shader =  composer.SelectedBlockShaderId.Value});
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
