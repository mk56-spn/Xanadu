// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using ZLinq;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
    public class NotelineSystem : QuerySystem<NoteEcs, ElementEcs>, IDisposable
    {
        private readonly RenderRid canvas = RenderRid.Create();

        public NotelineSystem()
            => canvas
                .SetModulate(Colors.White with {A = 0.4F})
                .SetParent(DiProvider.Get<IVisualsMaster>().GameplayerLayerRid);

        protected override void OnAddStore(EntityStore store)=>
            drawLine();

        private (Vector2 v, Color c)[] points = [];
        protected override void OnUpdate()
        {
                (Vector2 v, float timing, Color c)[] time = new (Vector2 v, float timing, Color c)[Query.Count];

                int i = 0;
                Query.ForEachEntity((ref NoteEcs component1, ref ElementEcs component2, Entity _) =>
                {
                    time[i] = (component2.Vector2, component1.TimingPoint, component1.NoteType.NoteColor());
                    i++;
                });

                points = time.AsValueEnumerable().OrderBy(c=> c.timing).Select(c=> (c.v, c.c)).ToArray();
            drawLine();
        }

        private void drawLine()
        {
            canvas.Clear();

            // Guard against bs.
            if (points.Length < 2) return;

            canvas.SetZIndex(-10);

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 startPoint = points[i].v;
                Vector2 endPoint = points[i + 1].v;

                Color opaque = points[i].c;

                Color[] segmentColors = [Colors.Transparent, opaque, Colors.Transparent];

                Vector2 midPoint = startPoint.Lerp(endPoint, 0.5f);
                Vector2[] segmentPoints = new[] { startPoint, midPoint, endPoint };
                canvas.AddPolyline(segmentPoints, segmentColors);
            }
        }

        public void Dispose()=>
            RenderingServer.FreeRid(canvas);
    }
}
