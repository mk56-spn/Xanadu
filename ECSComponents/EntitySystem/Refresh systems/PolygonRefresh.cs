// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class PolygonRefresh : BaseRefreshSystem<PolygonEcs>
    {
        protected override void OnUpdate()
        {
            foreach (var (elements, polygons, entities) in Query.Chunks)
                for (int n = 0; n < entities.Length; n++)
                    RenderingServer.CanvasItemAddPolygon(elements[n].Canvas, polygons[n].Points, []);
        }
    }
}
