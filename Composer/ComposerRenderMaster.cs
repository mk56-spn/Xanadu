// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Rendering;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.GD;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Composer
{
	public partial class ComposerRenderMaster : RenderMaster
	{
		private Vector2 heldMousePosition;
		public bool Snapped = false;

		public Action<Entity> Action = null!;
		private Color lastColour = Colors.White;

		private Rid lastSelected;
		public readonly ArchetypeQuery<ElementEcs, SelectionEcs> Selected;

		private ComponentIndex<SelectionEcs, Rid> index;


		private  Camera2D camera2D = new PanningCamera();

		public ComposerRenderMaster(SerializableStage serializableStage, TrackInfo trackInfo) : base(serializableStage,
			trackInfo)
		{
			CanvasLayer canvasLayer;
			AddChild(canvasLayer = new CanvasLayer());


			canvasLayer.AddChild(camera2D);
			canvasLayer.AddChild(ComposerVisuals.Create(this));

			Selected = EntityStore.Query<ElementEcs, SelectionEcs>().AllTags(Tags.Get<SelectionFlag>());

			SetAnchorsPreset(LayoutPreset.FullRect);
		}

		public event Action? SelectionChanged;

		public override void _EnterTree()
		{
			base._EnterTree();
			MouseFilter = MouseFilterEnum.Pass;

			index = EntityStore.ComponentIndex<SelectionEcs, Rid>();

			EntityStore.Query<ElementEcs>()
				.ForEachEntity((ref ElementEcs element, Entity entity) =>
				{
					var area = createSelectionArea(entity, element);
					entity.AddComponent(new SelectionEcs(area));
				});
		}

		#region Input handling

		public override void _UnhandledInput(InputEvent @event)
		{
			QueueRedraw();

			switch (@event)
			{
				case InputEventKey { CtrlPressed: true, KeyLabel: Key.V, Pressed: true }:
					addElement(Action);
					break;
				case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } when Selected.Count == 0:
					return;
				case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
					Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs selection, Entity entity) =>
					{
						PrintRich($"[code][color=red]Item {element.Canvas} removed");
						entity.RemoveTag<SelectionFlag>();
						SelectionChanged?.Invoke();
						removeEntity(entity, selection);
					});
					break;
				case InputEventMouseButton { ButtonIndex: MouseButton.Left }:
					SelectionChanged?.Invoke();

					selectPoint();
					break;
			}

			if (@event is not InputEventMouseMotion motion) return;

			if (Input.IsMouseButtonPressed(MouseButton.Left))

				Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity entity) =>
				{
					var newPos = element.Transform with
					{
						Origin = element.Transform.Origin + motion.Relative / camera2D.Zoom,
					};
					element.Transform = newPos;
					element.Draw(entity);
				});

		}

		#endregion

		public override void _Process(double delta)
		{
			base._Process(delta);

			if (TrackHandler.Playing)
			{
				camera2D.Offset = Vector2.Zero;
				camera2D.Position = camera2D.Position.Lerp(RenderCharacter.Position, (float)(2f * delta));
			}
			try
			{
				lastColour = Selected.Entities.FirstOrDefault().GetComponent<ElementEcs>().Colour;
			}
			catch (Exception)
			{
				// ignored
			}
		}

		private bool  selectPoint()
		{
			var query = queryPoint();

			SelectionChanged?.Invoke();


			switch (query.Length)
			{
				case 0:
					Selected.ForEachEntity((ref ElementEcs _, ref SelectionEcs _, Entity entity) =>
						entity.RemoveTag<SelectionFlag>());
					addElement(Action);
					return false;
				default:
					Selected.ForEachEntity((ref ElementEcs _, ref SelectionEcs _, Entity entity) =>
						entity.RemoveTag<SelectionFlag>());
					Print("Query length " + query.Length);

					Entity entity;

					if (query.Contains(lastSelected))
					{
						int indexOf = query.ToList().IndexOf(lastSelected);

						var first = query.ElementAtOrDefault(indexOf + 1) == default
							? query.First()
							: query.ElementAt(indexOf + 1);

						entity = index[first][0];
						Print(query.ElementAtOrDefault(indexOf + 1));
						lastSelected = first;
					}
					else
					{
						entity = index[query[0]][0];
						lastSelected = query.First();
					}

					entity.AddTag<SelectionFlag>();
					break;
			}

			return true;
		}

		private Rid[] queryPoint()
		{
			var query = new PhysicsPointQueryParameters2D
			{
				Position = GetLocalMousePosition(),
				CollideWithAreas = true,
				CollideWithBodies = false,
				CollisionMask = 0b00000000_00000000_00000000_01000000
			};

			return GetWorld2D().DirectSpaceState
				.IntersectPoint(query)
				.SelectMany(v => v.Values)
				.Select(c => c.Obj)
				.OfType<Rid>().ToArray();
		}


		private void removeEntity(Entity entity, SelectionEcs selection)
		{
			RenderingServer.FreeRid(entity.GetComponent<ElementEcs>().Canvas);
			FreeRid(selection.Area);

			if (entity.TryGetComponent(out HitZoneEcs hitZone))
				FreeRid(hitZone.Area);

			if (entity.TryGetComponent(out BlockEcs blockEcs))
				blockEcs.Remove();

			if (entity.TryGetComponent(out HurtZoneEcs hurtZone))
				FreeRid(hurtZone.Area);

			entity.DeleteEntity();
		}


		#region ElementCreation

		private void addElement(Action<Entity> action)
		{
			PrintRich("[code][color=green]Item added");

			Entity entity = EntityStore.CreateEntity();

			action.Invoke(entity);

			ref ElementEcs elementEcs = ref entity.GetComponent<ElementEcs>();

			elementEcs.CanvasCreate(BaseCanvas);

			elementEcs.Colour = lastColour;
			elementEcs.Draw(entity);

			PrintRich("[code][color=green]Element created " + elementEcs.Canvas);

			EntityStore.Query<BlockEcs, ElementEcs,RectEcs >().ForEachEntity((ref BlockEcs blockEcs, ref ElementEcs elementEcs, ref RectEcs rectEcs, Entity _) =>
			{
				if (blockEcs.Body.IsValid) return;
				blockEcs.Create(elementEcs, rectEcs, GetWorld2D());
			});


			// Create area
			var area = createSelectionArea(entity,  entity.GetComponent<ElementEcs>());
			entity.AddComponent(new SelectionEcs(area));
		}

		private Rid createSelectionArea(Entity entity, ElementEcs element)
		{
			var area = AreaCreate();

			Rid shape;

			switch (entity)
			{
				case var _ when entity.HasComponent<NoteEcs>():
					PrintRich("[code][color=orange]Circle");
					shape = CircleShapeCreate();
					ShapeSetData(shape, NoteEcs.RADIUS);
					break;

				case var _ when entity.HasComponent<RectEcs>():
					PrintRich("[code][color=yellow]Rectangle");
					shape = RectangleShapeCreate();
					ShapeSetData(shape, entity.GetComponent<RectEcs>().Extents / 2);
					break;

				case var _ when entity.HasComponent<PolygonEcs>():
					shape = RectangleShapeCreate();
					ShapeSetData(shape, new Vector2(54, 50));
					break;

				case var _ when entity.HasComponent<HurtZoneEcs>():
					shape = ConvexPolygonShapeCreate();
					ShapeSetData(shape, HurtZoneEcs.TRIANGLE);
					break;

				default:
					shape = CircleShapeCreate();
					ShapeSetData(shape, NoteEcs.RADIUS);
					break;
			}

			AreaAddShape(area, shape);
			AreaSetSpace(area, GetWorld2D().Space);
			AreaSetTransform(area, element.Transform);
			AreaSetCollisionLayer(area, 0b00000000_00000000_00000000_01000000);

			return area;
		}
		#endregion
	}
}
