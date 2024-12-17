// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox.Transform.Query.Ops;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.Rendering;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Composer
{
	public partial class ComposerRenderMaster  : RenderMaster
	{
		private bool held;
		private Vector2 heldMousePosition;

		public readonly ArchetypeQuery<ElementEcs, SelectionEcs> Selected;
		private ComponentIndex<SelectionEcs, Rid> index;

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
			base._EnterTree();
			MouseFilter = MouseFilterEnum.Pass;

			index = EntityStore.ComponentIndex<SelectionEcs, Rid>();

			EntityStore.Query<ElementEcs>()
				.ForEachEntity((ref ElementEcs element, Entity entity ) =>
				{
					Rid area = createSelectionArea(entity, element);
					entity.AddComponent(new SelectionEcs(area) { LastPosition = element.Transform.Origin });
				});
		}

		public override void _Ready()
		{
			base._Ready();
			GD.PrintRich("[code][color=pink] " + index[EntityStore.Query<SelectionEcs>().Entities.ToArray()[4].GetComponent<SelectionEcs>().Area]);

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
					Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs selection, Entity entity) =>
					{
						GD.PrintRich($"[code][color=red]Item {element.Canvas} removed");
						entity.RemoveTag<SelectionFlag>();
						removeEntity(entity, selection);
					});
					break;
			}

			if (@event is not InputEventMouse) return;

			if (held)
				Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs component2, Entity entity) =>
				{
					var newPos = element.Transform with { Origin = component2.LastPosition + GetGlobalMousePosition() - heldMousePosition};
					element.SetTransform(newPos);
					component2.SetTransform(newPos);

					if (entity.HasComponent<HitZoneEcs>())
						entity.GetComponent<HitZoneEcs>().SetTransform(newPos);

					if (entity.HasComponent<BlockEcs>())
						entity.GetComponent<BlockEcs>().SetTransform(newPos);

				} );

			if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left }) return;

			if (!held & @event.IsPressed())
			{
				Selected.ForEachEntity((ref ElementEcs component1, ref SelectionEcs component2, Entity entity) =>
				{
					component2.LastPosition = component1.Transform.Origin;
				});
				heldMousePosition = GetGlobalMousePosition();
			}


			held = @event.IsPressed();

			if (!@event.IsPressed()) return;

			selectPoint();
		}

		#endregion


		#region ElementCreation

		private void addElement()
		{
			GD.PrintRich("[code][color=green]Item added");
			QueueRedraw();
		}

		private Rid createSelectionArea(Entity entity, ElementEcs element)
		{
			var area = AreaCreate();

			Rid shape = default;

			if (entity.HasComponent<NoteEcs>())
			{
				GD.PrintRich("[code][color=orange]Circle");
				shape = CircleShapeCreate();
				ShapeSetData(shape, NoteEcs.RADIUS);
			}

			if (entity.HasComponent<RectEcs>())
			{
				GD.PrintRich("[code][color=yellow]Rectangle");
				shape = RectangleShapeCreate();
				ShapeSetData(shape, entity.GetComponent<RectEcs>().Extents / 2);
			}

			if (entity.HasComponent<PolygonEcs>())
			{
				shape = RectangleShapeCreate();
				ShapeSetData(shape, new Vector2(100,100));
			}

			AreaAddShape(area, shape );
			AreaSetSpace(area, GetWorld2D().Space);
			AreaSetTransform(area, element.Transform);
			AreaSetCollisionLayer(area, 0b00000000_00000000_00000000_01000000);

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
				Entity entity = index[first][0];

				GD.Print(query.ElementAtOrDefault(indexOf + 1));

				entity.AddTag<SelectionFlag>();
				lastSelected = first;
			}

			else
			{
				if (query.Length == 0) return;
				Entity e  = index[query[0]][0];
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
				CollisionMask =	0b00000000_00000000_00000000_01000000
			};

			return GetWorld2D().DirectSpaceState
				.IntersectPoint(query)
				.SelectMany(v => v.Values)
				.Select(c => c.Obj)
				.OfType<Rid>().ToArray();
		}


		private void removeEntity(Entity entity, SelectionEcs selection)
		{
			FreeRid(selection.Area);

			if (entity.HasComponent<HitZoneEcs>())
			/*	FreeRid(entity.GetComponent<AreaEcs>().Area);*/


			if (entity.HasComponent<ElementEcs>())
				RenderingServer.FreeRid(entity.GetComponent<ElementEcs>().Canvas);

			if (entity.HasComponent<BlockEcs>())
				entity.GetComponent<BlockEcs>().Remove();

			entity.DeleteEntity();
		}
	}
}
