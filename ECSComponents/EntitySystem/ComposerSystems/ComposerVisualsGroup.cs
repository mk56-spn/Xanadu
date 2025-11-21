// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using XanaduProject.ECSComponents.EntitySystem.ComposerSystems.Widgets;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class ComposerVisualsGroup : SystemGroup
        {
        public ComposerVisualsGroup(string name) : base(name)
        {
           Add(new RotationSystem());
           Add(new GridSystem());
           Add(new NoteDirectionWidgetSystem());
           Add(new NoteTypeWidgetSystem());
           Add(new BlockMaterialWidget());
           Add(new NoteButtonsSystem());
           Add(new ToggleButtonsSystem());
           Add(new MouseEntityRepresentation());
           Add(new NoteTimelineSystem());
           Add(new DragSelectWidgetSystem());
           Add(new ScalingSystem());
           Add(new DuplicationSystem());
        }
    }
}
