// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.GameDependencies;
using ZLinq;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
    public class NotelineSystem : QuerySystem<NoteEcs, ElementEcs>, IDisposable
    {
        private Rid canvas = RenderingServer.CanvasItemCreate();

        public NotelineSystem()=>
            RenderingServer.CanvasItemSetParent(canvas, DiProvider.Get<IVisualsMaster>().GameplayerLayerRid);


        protected override void OnAddStore(EntityStore store)
        {
            base.OnAddStore(store);
            drawLine();
        }

        private Vector2[] points = [];
        protected override void OnUpdate()
        {
                points = new Vector2[Query.Count];

                (Vector2 v, float timing)[] time = new (Vector2 v, float timing)[Query.Count];

                int i = 0;
                Query.ForEachEntity((ref NoteEcs component1, ref ElementEcs component2, Entity _) =>
                {
                    time[i] = (component2.Vector2, component1.TimingPoint);;
                    i++;
                });

                points = time.AsValueEnumerable().OrderBy(c=> c.timing).Select(c=> c.v).ToArray();


            drawLine();
        }

        private void drawLine()
        {
            if (points.Length <2 ) return;
            RenderingServer.CanvasItemClear(canvas);
            RenderingServer.CanvasItemAddPolyline(canvas,points,[]);
        }

        public void Dispose()=>
            RenderingServer.FreeRid(canvas);

    }
}
