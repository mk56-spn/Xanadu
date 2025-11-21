// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.Editor.Input;

namespace XanaduProject.Stage.Masters.Composer
{
    public class DragSelectWidgetSystem : QuerySystem
    {
        private readonly IComposer composer = DiProvider.Get<IComposer>();

        private readonly RenderRid canvas = RenderRid.Create();

        protected override void OnAddStore(EntityStore store)=>
                canvas.SetParent(DiProvider.Get<IVisualsMaster>().GameplayerLayerRid)
                    .SetZIndex(100);


        protected override void OnUpdate()
        {
            bool visible = composer.State == BaseInputHandler.InputState.Dragging && composer.IsDragSelecting;
            var size = composer.LastClickedMousePosLocal - composer.MousePosLocal;
            size = size.Abs();


            if (visible)
            {
                canvas.Clear()
                    .SetVisible(visible)
                    .AddRect(size, Colors.Blue with { A = 0.2F})
                    .AddRectOutline(size, Colors.Blue)
                    .SetTransform((composer.LastClickedMousePosLocal + composer.MousePosLocal) / 2);
            }

            else canvas.SetVisible(false);
        }
    }
}
