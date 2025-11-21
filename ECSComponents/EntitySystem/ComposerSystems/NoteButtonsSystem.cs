// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;
using XanaduProject.UiElements;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
	public class NoteButtonsSystem : QuerySystem
	{
		private static readonly IComposerVisuals visuals = DiProvider.Get<IComposerVisuals>();
		private static readonly GridContainer container = new() { Columns = 5 };

		private readonly int[] values =
        [
            1,2,4,8,16,
            -1,-2,-4,-8,-16
        ];

		private ArchetypeQuery<NoteEcs> query = null!;

		protected override void OnUpdate()
		{
			container.Visible = (query.Count != 0);
		}

		private readonly struct EachNote(int value) : IEach<NoteEcs>
		{
			private readonly IClock clock = DiProvider.Get<IClock>();

			public void Execute(ref NoteEcs note)
			{
				note.TimingPoint = clock.OffsetBeat(value,note.TimingPoint);
			}
		}

		protected override void OnAddStore(EntityStore store)
		{
			query = store.Query<NoteEcs>();
			query.Filter.AnyTags(Tags.Get<SelectionFlag>());

			visuals.EntityEditTabAdd(container, "Note timing");
			foreach (int value in values)
            {
                var s = new AnimatedHoverButton("1 /" + value.ToString(CultureInfo.InvariantCulture), 10)
                {
                    MainColour = Colors.White.Darkened(0.5f / value)
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
