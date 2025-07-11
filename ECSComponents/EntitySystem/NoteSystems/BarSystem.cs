// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.Tag;
using static Godot.RenderingServer;
using static XanaduProject.DataStructure.JudgementInfo;

namespace XanaduProject.ECSComponents.EntitySystem.NoteSystems
{
    public partial class BarSystem : QuerySystem<Hit, NoteEcs>
    {
        private readonly CanvasLayer canvas;
        private readonly Bar barNode = new();

        public BarSystem(CanvasLayer canvas)
        {
            this.canvas = canvas;

            Filter.AnyComponents(ComponentTypes.Get<Hit>());
        }

        protected override void OnUpdate() =>
            Query.Each(new HitProcessor(barNode));
        private readonly struct HitProcessor(Bar node) : IEach<Hit, NoteEcs>
        {
            public void Execute(ref Hit hit, ref NoteEcs note)
            {
                double deviation = (GlobalClock.Instance.PlaybackTimeSec - note.TimingPoint) * 1000;

                var lineCanvas = CanvasItemCreate();
                CanvasItemSetParent(lineCanvas, node.GetCanvasItem());
                CanvasItemAddLine(lineCanvas, new Vector2((float)(-hit.Time * 3), -10), new Vector2((float)(-hit.Time * 3), 10),
                    GetJudgmentColor(GetJudgement(deviation)), 5);

               /* node.GetTree().CreateTimer(0.5).Timeout += () => FreeRid(lineCanvas);*/
            }
        }

        public partial class Bar : Node2D
        {
            public override void _Ready()
            {
                Position = new Vector2(GetWindow().Size.X / 2f, 20);
            }

            public override void _Draw()
            {
                addLine(Judgement.Miss);
                addLine(Judgement.Terrible);
                addLine(Judgement.Deficient);
                addLine(Judgement.Fair);
                addLine(Judgement.Clean);
                addLine(Judgement.Flawless);
                addLine(Judgement.FlawlessP);
                return;

                void addLine(Judgement judgement)
                {
                    DrawLine(new Vector2(-(float)JudgementDeviation(judgement) * 3, 0),
                        new Vector2((float)JudgementDeviation(judgement) * 3, 0), GetJudgmentColor(judgement), 1);
                }
            }
        }
    }
}
