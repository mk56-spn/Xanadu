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

namespace XanaduProject.Composer
{
	public partial class ComposerRenderMaster : RenderMaster
	{
		public readonly ArchetypeQuery<ElementEcs, SelectionEcs> Selected;

		public bool Snapped = false;

		public ComposerRenderMaster(SerializableStage serializableStage, TrackInfo trackInfo) : base(serializableStage,
			trackInfo)
		{
			CanvasLayer canvasLayer;
			AddChild(canvasLayer = new CanvasLayer());
			AddChild(new ComposerMacros(this));


			canvasLayer.AddChild(new PanningCamera());
			canvasLayer.AddChild(ComposerVisuals.Create(this));

			Selected = EntityStore.Query<ElementEcs, SelectionEcs>().AllTags(Tags.Get<SelectionFlag>());

			SetAnchorsPreset(LayoutPreset.FullRect);

			}

		public event Action? AddElement;
		public event Action? SelectionChanged;

		public override void _EnterTree()
		{
			base._EnterTree();
			MouseFilter = MouseFilterEnum.Pass;

		}

		#region Input handling

		public override void _UnhandledInput(InputEvent @event)
		{
			leftClick(@event);
			rightClick(@event);

			if (@event is not InputEventMouseMotion motion) return;

			mouseDrag(motion);
		}

		private void rightClick(InputEvent @event)
		{
			if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } or ) return;
			EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).ForEachEntity((ref ElementEcs _, Entity entity) => entity.DeleteEntity());
		}

		private void leftClick(InputEvent @event)
		{
			if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;

			// We add the last place we clicked to a vector 2, this allows a soft "snapping" when moving items
			lastClickPos = GetLocalMousePosition();
			dragThresholdPassed = false;

			// First, we check for area collisions
			var rids = queryPoint();

			// If we don't find anything, we choose to deselect or add an element based on whether something is already selected
			if (rids.Length == 0)
			{
				if (Selected.Count != 0)
					deselect();
				else AddElement?.Invoke();

				return;
			}

			if (Input.IsKeyPressed(Key.Shift))
			{
				selectPoint(rids);
				return;
			}

			bool contains = Selected.Entities.Select(c=>c.GetComponent<SelectionEcs>().Area).Any(c => rids.Contains(c));

			if(contains) return;

			deselect();
			selectPoint(rids);

			SelectionChanged?.Invoke();
		}
		
		private Vector2 lastClickPos;
		private bool dragThresholdPassed;

		private const float threshold = 10;
		private void mouseDrag(InputEventMouseMotion motion)
		{
			if (!Input.IsMouseButtonPressed(MouseButton.Left))  return;

			if (lastClickPos.DistanceTo(GetLocalMousePosition()) < threshold && !dragThresholdPassed) return;

			Vector2 delta =  motion.Relative/ GetViewport().GetCamera2D().Zoom;

			applyDeltaToSelected(delta);

			dragThresholdPassed = true;
		}

		private void applyDeltaToSelected(Vector2 delta)
		{
			Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity _) =>
			{
				if (!dragThresholdPassed)
					element.Transform.Origin +=  GetLocalMousePosition() - lastClickPos;

				element.Transform.Origin += delta;
			});
		}

		#endregion


		private void selectPoint(Rid[] rids)
		{
			var command = EntityStore.GetCommandBuffer();

			if (Input.IsKeyPressed(Key.Shift))
			{
				foreach (var areaRid in rids)
				{
					EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
						.ForEachEntity((ref ElementEcs _, Entity entity) =>
							command.AddTag<SelectionFlag>(entity.Id));
				}
			}
			else
			{
				var areaRid = rids[0];
				EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
					.ForEachEntity((ref ElementEcs _, Entity entity) =>
						command.AddTag<SelectionFlag>(entity.Id));
			}

			command.Playback();
		}

		private void deselect()
		{
			EntityBatch batch = new EntityBatch();
			batch.RemoveTag<SelectionFlag>();
			EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).Entities.ApplyBatch(batch);
		}


		private Rid[] queryPoint()
		{
			var query = new PhysicsPointQueryParameters2D
			{
				Position = GetLocalMousePosition(),
				CollideWithAreas = true,
				CollideWithBodies = false,
				CollisionMask = 0b00000000_00000000_00000000_01000000,

			};

			return GetWorld2D().GetDirectSpaceState().IntersectPoint(query, 100)
				.Select(hitResult => (Rid)hitResult["rid"]).ToArray();
		}
	}
}
