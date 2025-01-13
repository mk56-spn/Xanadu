// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using Godot;
using XanaduProject.Composer;
using XanaduProject.ECSComponents.Interfaces;
using static Godot.RenderingServer;
using IComponent = Friflo.Engine.ECS.IComponent;

namespace XanaduProject.ECSComponents
{
    public struct ElementEcs() : IComponent
    {
        public Transform2D Transform = Transform2D.Identity;

        [Composer("Colour")] public Color Colour = Colors.Olive;

        [Composer("Index")]
        // ReSharper disable once MemberCanBePrivate.Global
        public int Index = 0;

        [Ignore] public Rid Canvas;

        [Ignore] public Vector2 Size = new(32, 32);

        public void UpdateCanvas(Color colour)
        {
            CanvasItemSetModulate(Canvas, colour);
        }

        public void SetTransform(Transform2D transform2D)
        {
            Transform = transform2D;
            CanvasItemSetTransform(Canvas, Transform);
        }

        public static Color ComposerColour = Colors.Red;


        public void SetDepth(int value)
        {
            GD.Print("calledS");
            Index = value;
            CanvasItemSetZIndex(Canvas, value);
        }

        public void SetRotation(float rotation)
        {
            Transform = Transform.RotatedLocal(rotation - Transform.Rotation);
            CanvasItemSetTransform(Canvas, Transform);
        }

        public void SetScale(Vector2 value)
        {
            Transform = Transform.ScaledLocal(value - Transform.Scale);
        }

        public void CanvasCreate(Rid baseCanvas)
        {
            Canvas = CanvasItemCreate();
            CanvasItemSetParent(Canvas, baseCanvas);
        }


        /// <summary>
        ///     Refreshes the entire entity, should not be used outside of composer as its full of if checks
        /// </summary>
        /// <param name="entity"></param>
        public void Draw(Entity entity)
        {
            CanvasItemClear(Canvas);
            CanvasItemSetModulate(Canvas, Colour);
            CanvasItemSetZIndex(Canvas, Index);
            CanvasItemSetTransform(Canvas, Transform);


            @try<RectEcs>(ref entity);
            @try<PolygonEcs>(ref entity);
            @try<SelectionEcs>(ref entity);
            @try<BlockEcs>(ref entity);
            @try<NoteEcs>(ref entity);
            @try<HitZoneEcs>(ref entity);
            @try<ParticlesEcs>(ref entity);
            @try<HurtZoneEcs>(ref entity);
        }

        private void @try<T>(ref Entity entity)
            where T : struct, IComponent, IUpdatable
        {
            if (entity.TryGetComponent<T>(out var t))
                t.Update(this);
        }
    }
}
