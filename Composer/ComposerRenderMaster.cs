// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox.Transform.Query.Ops;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.Rendering;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Composer
{
	public partial class ComposerRenderMaster  : RenderMaster
	{
		public static readonly Color COMPOSER_ACCENT = Colors.DeepPink;

		public int SelectedTexture;

		private bool held;
		private Vector2 heldMousePosition;

		public readonly ArchetypeQuery<ElementEcs, SelectionEcs> Selected;
		public ComponentIndex<SelectionEcs, Rid> Index;

		public ComposerRenderMaster(SerializableStage serializableStage, TrackInfo trackInfo) : base(serializableStage,
			trackInfo)
		{
			CanvasLayer canvasLayer;
			AddChild(canvasLayer = new CanvasLayer());
			canvasLayer.AddChild(new PanningCamera());
			canvasLayer.AddChild(ComposerVisuals.Create(this));

			Selected = EntityStore.Query<ElementEcs,SelectionEcs>().AllTags(Tags.Get<SelectionFlag>());

			SetAnchorsPreset(LayoutPreset.FullRect);
		}


		public override void _EnterTree()
		{
			MouseFilter = MouseFilterEnum.Pass;

			Index = EntityStore.ComponentIndex<SelectionEcs, Rid>();

			EntityStore.Query<ElementEcs>()
				.ForEachEntity((ref ElementEcs element, Entity entity ) =>
				{
					Rid area = createSelectionArea(element.Transform);
					entity.AddComponent(new SelectionEcs(area));
				});
		}

		#region Input handling

		public override void _UnhandledInput(InputEvent @event)
		{
			QueueRedraw();

			switch (@event)
			{
				case InputEventKey { CtrlPressed: true, KeyLabel: Key.V, Pressed: true}:
					addElement();
					break;
				case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } when Selected.Count == 0:
					return;
				case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
					Selected.ForEachEntity((ref ElementEcs _, ref SelectionEcs _, Entity entity) => entity.RemoveTag<SelectionFlag>());
					break;
			}

			if (@event is not InputEventMouse) return;

			if (held)
				Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs component2, Entity _) =>
				{
					var newPos = element.Transform with { Origin = GetGlobalMousePosition()};
					element.SetTransform(newPos);
					component2.SetTransform(newPos);
				} );

			if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left }) return;

			if (!held & @event.IsPressed())
				heldMousePosition = GetLocalMousePosition();

			held = @event.IsPressed();

			if (!@event.IsPressed()) return;

			selectPoint();
		}

		#endregion


		#region ElementCreation

		private void addElement()
		{
			GD.PrintRich("[code][color=green]Item added");

			Element element = new NoteElement
			{
				Group = 1,
				Position = GetLocalMousePosition().Snapped(new Vector2(32, 32)),
				Rotation = 0,
				Scale = Vector2.One,
				TimingPoint = 0.3f
			};


			var entity = CreateItem2(element);
			var area = createSelectionArea(element.Transform);


			QueueRedraw();
		}

		private Rid createSelectionArea(Transform2D transform2D)
		{
			var area = AreaCreate();
			var shape = RectangleShapeCreate();

			AreaSetSpace(area, GetWorld2D().Space);
			AreaAddShape(area, shape);
			ShapeSetData(shape, new Vector2(64,64) / 2);

			AreaSetCollisionLayer(area, 1);

			AreaSetTransform(area, transform2D);
			AreaSetCollisionLayer(area, 0b001);

			return area;
		}

		private void removeElement(ElementEcs elementEcs)
		{
			GD.PrintRich("[code][color=red]Item removed");
			QueueRedraw();
		}

		#endregion

		private Rid lastSelected;

		private void selectPoint()
		{
			Rid[] query = queryPoint();

			if (query.Length == 0 && Selected.Count == 0)
			{
				addElement();
				return;
			}

			Selected.ForEachEntity((ref ElementEcs _, ref SelectionEcs _,Entity entity) => entity.RemoveTag<SelectionFlag>());

			GD.Print("Query length " + query.Length);

			if (query.Contains(lastSelected))
			{
				int indexOf = query.ToList().IndexOf(lastSelected);

				Rid first = query.ElementAtOrDefault(indexOf + 1) == default ? query.First() : query.ElementAt(indexOf + 1);
				Entity entity = Index[first][0];

				GD.Print(query.ElementAtOrDefault(indexOf + 1));

				entity.AddTag<SelectionFlag>();
				lastSelected = first;
			}

			else
			{
				Entity e  = Index[query[0]][0];

				e.AddTag<SelectionFlag>();
				lastSelected = query.First();
			}
		}

		private Rid[] queryPoint()
		{
			var query = new PhysicsPointQueryParameters2D
			{
				Position = GetLocalMousePosition(),
				CollideWithAreas = true,
				CollideWithBodies = false,
			};

			return GetWorld2D().DirectSpaceState
				.IntersectPoint(query)
				.SelectMany(v => v.Values)
				.Select(c => c.Obj)
				.OfType<Rid>().ToArray();
		}
	}
}
