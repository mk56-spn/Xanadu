// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.Composer
{
	internal partial class ButtonContainer : VBoxContainer
	{
		private readonly EntityStore pseudoStore = new();
		private ButtonGroup buttonGroup = null!;
		public ComposerRenderMaster Composer = null!;

		private Entity targetEntity;

		public ButtonContainer()
		{
			pseudoStore.CreateEntity(new NoteEcs(NoteType.Main), new NameEcs("Note Main"));

			pseudoStore.CreateEntity(new NoteEcs(NoteType.Main){ TimingPoint = 3}, new DirectionEcs(), new NameEcs("Note Directional"));
			pseudoStore.CreateEntity(new NoteEcs(NoteType.R1){ TimingPoint = 3}, new NameEcs("Note R1"));
			pseudoStore.CreateEntity(new NoteEcs(NoteType.R2){ TimingPoint = 3}, new NameEcs("Note R2"));
			pseudoStore.CreateEntity(new TriangleArrayEcs(), new NameEcs("Array"));
			pseudoStore.CreateEntity(new PolygonEcs(), new NameEcs("Polygon"));
			pseudoStore.CreateEntity(new HurtZoneEcs(), new NameEcs("HurtZone"));

			for (int index = 0; index < RectEcs.PRESETS.Length; index++)
			{
				var preset = RectEcs.PRESETS[index];

				var ent = pseudoStore.CreateEntity(new RectEcs(preset), new BlockEcs(), new NameEcs("block" + preset));
				if (index == 0)
					targetEntity = ent;
			}

		}

		private Vector2 position(Vector2 size) => (Composer.GetLocalMousePosition() + size / 2).Snapped(size) - size / 2;


		public override void _Ready()
		{
			buttonGroup = new ButtonGroup { AllowUnpress = false };

			pseudoStore.Query<NameEcs>().ForEachEntity((ref NameEcs component1, Entity entity) =>
			{
				var b = new ItemButton(component1.Name, entity) { ButtonGroup = buttonGroup };
				b.Pressed += () => { targetEntity = b.Entity; };
				AddChild(b);
			});

			buttonGroup.GetButtons().First().ButtonPressed = true;

			Composer.AddElement += () => {
				var ent = Composer.EntityStore.CreateEntity();
				targetEntity.CopyEntity(ent);
				ent.AddTag<UnInitialized>();
				ent.AddTag<SelectionFlag>();


				Vector2 size = new Vector2(32, 32);

				if (ent.TryGetComponent(out RectEcs rect))
				{
					size = rect.Extents;
				}

				ent.AddComponent( new ElementEcs { Transform = Transform2D.Identity with { Origin = position(size) }});
			};
		}


		private partial class ItemButton : Button
		{
			public readonly Entity Entity;

			public ItemButton(string text, Entity entity)
			{
				Entity = entity;
				Text = text;
				CustomMinimumSize = new Vector2(0, 50);
				MouseExited += () =>
					CreateTween().TweenProperty(this, "modulate", Colors.White.Darkened(0.2f), 0.3f);
				MouseEntered += () => CreateTween().TweenProperty(this, "modulate", Colors.White, 0.2f);
			}

			public override void _Process(double delta)
			{
				base._Process(delta);

				if (ButtonGroup.GetPressedButton() == null) return;
				SelfModulate = ButtonGroup.GetPressedButton().Equals(this)
					? Colors.White
					: Colors.Black.Lightened(0.4f);
			}
		}
	}
}
