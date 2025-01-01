// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;
using XanaduProject.Screens;
using static Godot.RenderingServer;
using static XanaduProject.DataStructure.JudgementInfo;

namespace XanaduProject.Audio
{
	public partial class NoteProcessor(TrackHandler trackHandler, RenderCharacter renderCharacter, EntityStore entityStore) : Node2D
	{
		private ComponentIndex<HitZoneEcs, Rid> index;
		public event Action<float, Judgement>? Result;

		private CanvasLayer canvasLayer = new();

		private List<double> deviations = [];
		private List<Judgement> judgements = [];

		private float endTime = 0;

		public override void _EnterTree()
		{
			AddChild(canvasLayer);

			canvasLayer.AddChild(new Bar(this));

			index = entityStore.ComponentIndex<HitZoneEcs, Rid>();


		}

		public override void _Ready()
		{

			GD.Print(OS.GetExecutablePath().GetBaseDir().PathJoin("Stages") );

			base._Ready();

			trackHandler.OnSongCommence += () =>
			{
				foreach (var variable in canvasLayer.GetChildren().OfType<Results>())
				{
					variable.QueueFree();
				}
			};

			trackHandler.Stopped += () =>
			{
				deviations = [];
				judgements = [];

				entityStore.Query<NoteEcs>().AllTags(Tags.Get<Hit>())
					.ForEachEntity((ref NoteEcs _, Entity entity) =>
						entity.RemoveTag<Hit>());
			};
		}

		public override void _Process(double delta)
		{
			base._Process(delta);

			if (entityStore.Count == 0 ) return;
			endTime = entityStore.Query<NoteEcs>().Entities.MaxBy(c => c.GetComponent<NoteEcs>().TimingPoint)
				.GetComponent<NoteEcs>().TimingPoint;

			entityStore.Query<NoteEcs, ElementEcs, ParticlesEcs>().Each(new UpdateNote(trackHandler));

			if (!(endTime  + 2 < trackHandler.TrackPosition)) return;
			var devTemp = deviations;
			var judgeTemp = judgements;
			trackHandler.StopTrack();
			canvasLayer.AddChild(Results.Create(devTemp.ToArray(), judgeTemp.ToArray()));

		}

		public override void _Input(InputEvent @event)
		{
			if (!@event.IsActionPressed("main")) return;

			var query = renderCharacter.QueryShape();

			GD.PrintRich(["[code][color=orange]Returned note count is[color=green] " + query.Length]);
			Rid minBy;

			try {
				minBy = query
					.MinBy(c => index[c][0].GetComponent<NoteEcs>().TimingPoint);

				if (Mathf.Abs(index[minBy][0].GetComponent<NoteEcs>().TimingPoint - trackHandler.TrackPosition) > 0.3f)
				{
					return;
				}
			}

			catch (Exception) {
				return;
			}

			Entity noteEntity = index[minBy][0];

			ref NoteEcs  note = ref noteEntity.GetComponent<NoteEcs>();
			ref ElementEcs element = ref noteEntity.GetComponent<ElementEcs>();

			addResult(ref note, ref element);

			note.UpdateCharacter(renderCharacter, element);
			noteEntity.AddTag<Hit>();

			/*if (noteEntity.HasComponent<HoldEcs>())
			{
				renderCharacter.Velocity = Vector2.Zero;
				CreateTween().TweenProperty(renderCharacter, "position", noteEntity.GetComponent<HoldEcs>().EndPosition + element.Transform.Origin, noteEntity.GetComponent<HoldEcs>().Duration);
			}*/
		}

		private void addResult(ref NoteEcs note,  ref ElementEcs element)
		{
			double deviation = (note.TimingPoint - trackHandler.TrackPosition) *1000;
			GD.PrintRich("[code][color=orange]Deviation is[color=green] " + deviation);
			var judgement = GetJudgement(Mathf.Abs(deviation));
			string judgeText = GetJudgmentText(judgement);

			Rid resultCanvas = CanvasItemCreate();

			CanvasItemSetParent(resultCanvas, GetCanvasItem());

			Color color = GetJudgmentColor(judgement);

			Result?.Invoke((float)deviation, judgement);

			var size = ThemeDB.FallbackFont.GetStringSize(judgeText, fontSize: 50);

			ThemeDB.FallbackFont.DrawString(resultCanvas, element.Transform.Origin - size/ 2,
				GetJudgmentText(judgement) + (deviation < 0 ? " late" : " early"),
				modulate: color, fontSize:50);

			CreateTween().TweenMethod(
				Callable.From<Color>( c=> CanvasItemSetModulate(resultCanvas,c)),
				Colors.White, Colors.Transparent,0.5f);


			GetTree().CreateTimer(0.7f).Timeout += () => FreeRid(resultCanvas);

			deviations.Add(deviation);
			judgements.Add(judgement);
		}

		public readonly struct UpdateNote(TrackHandler trackHandler): IEach<NoteEcs, ElementEcs, ParticlesEcs>
		{
			public void Execute(ref NoteEcs noteEcs, ref ElementEcs element, ref ParticlesEcs particlesEcs)
			{
			  var color  = element.Colour
					with { A = 1 - 2 *  (float)Mathf.Abs(trackHandler.TrackPosition - noteEcs.TimingPoint) };

				element.UpdateCanvas(color);
			}
		}

		public partial class Bar(NoteProcessor noteProcessor) : Node2D
		{
			public override void _Ready()
			{
				base._Ready();

				Position = new Vector2(GetWindow().Size.X / 2f, 20);
				noteProcessor.Result += (f, j) =>
				{

					var lineCanvas = CanvasItemCreate();
					CanvasItemSetParent(lineCanvas, GetCanvasItem());
					CanvasItemAddLine(lineCanvas, new Vector2(-f * 3, -10), new Vector2(-f * 3, 10) , GetJudgmentColor(j), 5);

					GetTree().CreateTimer(0.7f).Timeout += () =>
					{
						FreeRid(lineCanvas);
					};
				};
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

				void addLine(Judgement judgement) {
					DrawLine(new Vector2(- (float)JudgementDeviation(judgement) * 3, 0),
						new Vector2((float)JudgementDeviation(judgement) * 3, 0), GetJudgmentColor(judgement), 1);
				}
			}
		}
	}
}
