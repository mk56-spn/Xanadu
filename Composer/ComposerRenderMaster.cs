// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.Rendering;
using XanaduProject.Serialization.Elements;
using XanaduProject.Serialization.SerialisedObjects;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Composer
{
	public partial class ComposerRenderMaster  : RenderMaster
	{
		public static readonly Color COMPOSER_ACCENT = Colors.DeepPink;

		public List<(RenderElement renderElement, Vector2 position)> SelectedAreas = [];



		private readonly Dictionary<Rid, RenderElement> areaHash = new();

		public int SelectedTexture;

		private bool held;
		private Vector2 heldMousePosition;

		public ComposerRenderMaster(SerializableStage serializableStage, TrackInfo trackInfo) : base(serializableStage,
			trackInfo)
		{
			CanvasLayer canvasLayer;
			AddChild(canvasLayer = new CanvasLayer());
			canvasLayer.AddChild(new PanningCamera());
			canvasLayer.AddChild(ComposerVisuals.Create(this));

			SetAnchorsPreset(LayoutPreset.FullRect);
		}

		public override void _EnterTree()
		{
			MouseFilter = MouseFilterEnum.Pass;

			foreach (var renderElement in RenderElements)
				renderElement.Area = createArea(renderElement.Element);

			foreach (var renderElement in RenderElements)
				areaHash.Add(renderElement.Area, renderElement);
		}

		#region Input handling

		public override void _UnhandledInput(InputEvent @event)
		{
			QueueRedraw();

			if (@event is InputEventKey { CtrlPressed: true, KeyLabel: Key.V, Pressed: true})
			{
				addElement();
			}

			if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
			{
				if (SelectedAreas.Count == 0) return;

				foreach (var renderElement in SelectedAreas)
					removeElement(renderElement.Item1);

				SelectedAreas = [];
			}

			if (@event is not InputEventMouse) return;

			if (held)
				foreach (var area in SelectedAreas)
					area.renderElement.SetPosition(area.Item2 + (GetLocalMousePosition() - heldMousePosition));

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

			var canvas = CreateItem(element);
			var area = createArea(element);

			NoteProcessor.Notes.Add(new Note((element as NoteElement)!, canvas));

			var renderElement = new RenderElement(element, canvas, area);

			RenderElements.Add(renderElement);
			areaHash.Add(area, renderElement);

			QueueRedraw();
		}

		private Rid createArea(Element element)
		{
			var area = AreaCreate();
			var shape = RectangleShapeCreate();

			var transform = element.Transform;

			AreaSetSpace(area, GetWorld2D().Space);
			AreaAddShape(area, shape);
			ShapeSetData(shape, element.Size() / 2);

			AreaSetCollisionLayer(area, 1);

			AreaSetTransform(area, transform);
			AreaSetCollisionLayer(area, 0b001);

			return area;
		}

		private void removeElement(RenderElement renderElement)
		{
			GD.PrintRich("[code][color=red]Item removed");
			renderElement.Remove();
			RenderElements.Remove(renderElement);
			QueueRedraw();
		}

		#endregion

		private Rid lastSelected;

		private void selectPoint()
		{
			Rid[] query = queryPoint();

			if (query.Length == 0 && SelectedAreas.Count == 0)
			{
				addElement();
				return;
			}

			SelectedAreas = [];

			GD.Print("Query length " + query.Length);

			if (query.Contains(lastSelected))
			{
				int i = query.ToList().IndexOf(lastSelected);

				Rid selected = query.ElementAtOrDefault(i + 1) == default ? query.First() : query.ElementAt(i + 1);
				RenderElement renderElement = areaHash[selected];

				GD.Print(query.ElementAtOrDefault(i + 1));

				SelectedAreas.Add((renderElement, renderElement.Element.Position));
				lastSelected = selected;
			}

			else
			{
				RenderElement renderElement = areaHash[query.First()];

				SelectedAreas.Add((renderElement, renderElement.Element.Position));
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
