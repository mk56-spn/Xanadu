// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class NoteBaseSystem(TrackHandler trackHandler): QuerySystem<NoteEcs, ElementEcs, TempValuesEcs>
    {
        protected override void OnUpdate() {
            Query.Each(new UpdateNote(trackHandler));
        }

        private readonly struct UpdateNote(TrackHandler trackHandler) : IEach<NoteEcs, ElementEcs, TempValuesEcs>
        {
            public void Execute(ref NoteEcs noteEcs, ref ElementEcs element, ref TempValuesEcs tempValuesEcs)
            {
                var color = element.Colour with
                {
                    A = 2 - 2 * (float)Mathf.Abs(trackHandler.TrackPosition - noteEcs.TimingPoint)
                };
                element.UpdateCanvas(color);

                float modScale = (float)Mathf.PosMod(trackHandler.TrackPosition, trackHandler.SecondsPerBeat);

                tempValuesEcs.Transform2D = tempValuesEcs.Transform2D.ScaledLocal(Vector2.One + new Vector2(modScale, modScale)); }
        }
    }
}
