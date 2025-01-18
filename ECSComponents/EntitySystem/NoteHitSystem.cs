// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class NoteHitSystem(TrackHandler trackHandler) : QuerySystem<Hit,ElementEcs, TempValuesEcs>
    {
        protected override void OnUpdate()=>
            Query.Each(new UpdateHitNote(trackHandler));
    }

    public readonly struct UpdateHitNote(TrackHandler trackHandler) : IEach<Hit, ElementEcs,TempValuesEcs>
    {
        public void Execute(ref Hit hit, ref ElementEcs element, ref TempValuesEcs tempValues )
        {
            float value = (float)Mathf.Ease(Mathf.Clamp(2f * (trackHandler.TrackPosition - hit.Time), 0f, 1f), 0.2);
            var scale = new Vector2(value, value);

            tempValues.Transform2D *=  tempValues.Transform2D.Scaled(scale);
            element.UpdateCanvas(element.Colour with { A = 1 - value });
        }
    }
}
