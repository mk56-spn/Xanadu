// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
	public class NoteButtonsSystem : QuerySystem
	{
		private static readonly IComposerVisuals visuals = DiProvider.Get<IComposerVisuals>();
		private static readonly GridContainer container = new() { Columns = 3 };

		private readonly float[] values =
		[
			-1,
			-1 / 2f,
			-1 / 4f,
			1 / 4f,
			1 / 2f,
			1
		];

		private ArchetypeQuery<NoteEcs> query = null!;

		protected override void OnUpdate()
		{
			container.Visible = (query.Count != 0);
		}

		private readonly struct EachNote(float value) : IEach<NoteEcs>
		{
			private readonly IClock clock = DiProvider.Get<IClock>();

			public void Execute(ref NoteEcs note)
			{
				note.TimingPoint =
					(float)(Mathf.Snapped(note.TimingPoint, 60 / clock.CurrentBpm * 0.25) +
							60 / clock.CurrentBpm * value);
			}
		}

		protected override void OnAddStore(EntityStore store)
		{
			query = store.Query<NoteEcs>();
			query.Filter.AnyTags(Tags.Get<SelectionFlag>());

			visuals.EntityEditTabAdd(container, "Note timing");
			foreach (float value in values)
			{
				var s = new Button
				{
					Text = value.ToString(CultureInfo.InvariantCulture)
				};

				s.Pressed += () =>
				{
					query.Each(new EachNote(value));
				};

				container.AddChild(s);
			}
		}

	}
}
