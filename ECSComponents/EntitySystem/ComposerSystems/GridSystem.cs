// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class GridSystem : ComposerSystem
    {
        private readonly int lineCount = 100;
        private readonly int spacing = 32;

        private readonly IVisualsMaster master = DiProvider.Get<IVisualsMaster>();
        private readonly Rid canvasItem = RenderingServer.CanvasItemCreate();

        public GridSystem()
        {
            var lineY = new Vector2[lineCount * 2];
            var lineX = new Vector2[lineCount * 2];


            for (int i = 0; i < lineCount; i++)
            {
                lineY[i * 2] = new Vector2(i * spacing, 0);
                lineY[i * 2 + 1] = new Vector2(i * spacing, 3000);
                lineX[i * 2] = new Vector2(0, i * spacing);
                lineX[i * 2 + 1] = new Vector2(5000, i * spacing);
            }

            canvasItem.AsRenderRid()
                .SetParent(master.GameplayerLayerRid)
                .SetModulate(Colors.Blue with { A = 0.2F })
                .SetZIndex(-10)
                .AddMultiline(lineX)
                .AddMultiline(lineY);
        }

        private static readonly Vector2 offset = new(1000, 1000);

        protected override void OnUpdate()=>
            canvasItem.AsRenderRid()
                .SetTransform(new Transform2D(0,(master.CameraPosition - offset).Snapped(spacing)));
    }
}
