// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;
using static Godot.PhysicsServer2D;

namespace XanaduProject.Factories
{
    public static class PhysicsFactory
    {
        private static World2D world => DiProvider.Get<IPhysicsMaster>().World2D;

        public const uint PLAYER_AREA_FLAG = 1 << 0 | 1 << 2 | 1 << 3;
        private const uint note_area_flag = 1 << 4 ;
        public const uint DAMAGE_AREA = 1 << 5;
        private const uint selection_area = 1 << 6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static  PhysicsRid AsPhysicsRid(this in Rid rid) => new(rid);

        public struct PhysicsRid(Rid rid)
        {
            public Rid Rid = rid;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Rid(PhysicsRid r) => r.Rid;
        }


        public static Rid CreateBodyRectangle(Transform2D transform, Vector2 size)
        {
            var body = BodyCreate();
            var shape = RectangleShapeCreate();

            BodySetSpace(body, world.Space);
            BodyAddShape(body, shape);
            ShapeSetData(shape, size / 2);

            BodySetCollisionLayer(body, PLAYER_AREA_FLAG);
            BodySetCollisionMask(body, PLAYER_AREA_FLAG);
            BodySetShapeTransform(body, 0, transform);
            BodySetMode(body, BodyMode.Static);

            return body;
        }



        public static Rid CreateHurtAreaRound()
        {
            var area = AreaCreate();
            var shape = CircleShapeCreate();

            AreaSetSpace(area, world.Space);
            AreaAddShape(area, shape);
            ShapeSetData(shape, 32);

            AreaSetMonitorable(area, true);
            AreaSetCollisionLayer(area, DAMAGE_AREA);

            return area;
        }

        public static Rid CreateNoteArea(Transform2D transform)
        {
            var area = AreaCreate();
            var shape = CircleShapeCreate();

            AreaSetSpace(area, world.Space);
            AreaAddShape(area, shape);
            ShapeSetData(shape,  NoteEcs.RADIUS);

            AreaSetTransform(area, transform);
            AreaSetCollisionLayer(area, note_area_flag);

            return area;
        }

        /// <summary>
        /// Creates a selection area in the physics space based on the provided entity and its associated components.
        /// </summary>
        /// <param name="entity">The entity containing component data that defines the shape of the selection area.</param>
        /// <param name="element">The element specifying the transformation properties for the selection area.</param>
        /// <returns>A RID representing the created selection area in the physics world.</returns>
        public static Rid CreateSelectionArea(Entity entity, ElementEcs element)
        {
            var area = AreaCreate();

            Rid shape;

            switch (entity)
            {
                case var _ when entity.HasComponent<RectEcs>():
                    GD.PrintRich("[code][color=yellow]Rectangle");
                    shape = RectangleShapeCreate();
                    ShapeSetData(shape, entity.GetComponent<RectEcs>().Extents / 2);
                    break;

                case var _ when entity.HasComponent<PolygonEcs>():
                    shape = ConvexPolygonShapeCreate();
                    ShapeSetData(shape, entity.GetComponent<PolygonEcs>().Points);
                    break;

                case var _ when entity.HasComponent<HurtZoneEcs>():
                    shape = ConvexPolygonShapeCreate();
                    ShapeSetData(shape, HurtZoneEcs.TRIANGLE);
                    break;

                case var _ when entity.HasComponent<NoteEcs>():
                default:
                    shape = CircleShapeCreate();
                    ShapeSetData(shape, NoteEcs.RADIUS);
                    break;
            }


            AreaAddShape(area, shape);
            AreaSetSpace(area, world.Space);
            AreaSetTransform(area, element.Transform);
            AreaSetCollisionLayer(area, selection_area);

            return area;
        }

        /// <summary>
        /// Performs a shape query in the 2D physics world and retrieves a list of area RIDs that the shape intersects with.
        /// </summary>
        /// <param name="shape">The 2D shape used for the query.</param>
        /// <returns>An array of RIDs representing the areas that the shape intersects with.</returns>
        public static Rid[] QueryNotesAreas(Shape2D shape)
        {
            var query = new PhysicsShapeQueryParameters2D
            {
                Transform = Transform2D.Identity with { Origin = DiProvider.Get<IPlayerCharacter>().Position },
                Shape = shape,
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = note_area_flag
            };

            return world.DirectSpaceState.IntersectShape(query)
                .Select(hitResult => (Rid)hitResult["rid"]).ToArray();
        }

        public static Rid[] QuerySelectionAreasPoint()
        {
            var query = new PhysicsPointQueryParameters2D
            {
                Position = DiProvider.Get<IComposer>().MousePosLocal,
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = selection_area
            };


            return DiProvider.Get<IPhysicsMaster>().World2D.GetDirectSpaceState().IntersectPoint(query, 100)
                .Select(hitResult => (Rid)hitResult["rid"]).ToArray();
        }

    }
}
