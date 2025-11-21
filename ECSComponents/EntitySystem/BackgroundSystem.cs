// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using ECSComponents;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Tools;

namespace XanaduProject.ECSComponents.EntitySystem
{
    public class BackgroundSystem : QuerySystem
    {
        private readonly CanvasLayer staticLayer = new()
        {
            FollowViewportEnabled = false,
            Layer = -10
        };

        private readonly RenderRid canvas = RenderRid.Create()
            .SetModulate(Colors.White.Darkened(0.7f));

        private static readonly ParticleProcessMaterial light_ray = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1000,0,0),
            AngleMin = 40, AngleMax = 45,
            ParticleFlagDisableZ = true,
            ScaleMin = 1, ScaleMax = 1.4f,
            ColorInitialRamp = ParticlesRidExtensions.FADE_GRADIENT,
            ColorRamp = new GradientTexture1D() {
                Gradient = new Gradient() {
                    Offsets = [0, .5f, 1],
                    Colors = [Colors.Transparent, Colors.White, Colors.Transparent] } }
        };
        private static readonly ParticleProcessMaterial block_material = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1000,0,0),
            InitialVelocity = new Vector2(0,0),
            ParticleFlagDisableZ = true,
            AngleMin = 40 , AngleMax = 50,
            ScaleMin = 1, ScaleMax = 4f,
            Gravity = Vector3.Zero,
            ColorInitialRamp = SimpleGradientTexture1D
                .CreateGradient(Colors.White, Colors.White.Darkened(0.3f))

        };

        private static readonly ParticleProcessMaterial dust_particle_material = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1000,1000,0),
            InitialVelocity = new Vector2(0,0),
            ScaleMin = 0.5f, ScaleMax = 2f,
            ColorRamp = new GradientTexture1D() {
                Gradient = new Gradient() {
                    Offsets = [0, .5f, 1],
                    Colors = [Colors.Transparent, Colors.White, Colors.Transparent] } },
            Gravity = Vector3.Zero,
            ColorInitialRamp = SimpleGradientTexture1D
                .CreateGradient(Colors.White, Colors.White.Darkened(0.3f))
        };

        private static readonly QuadMesh light_ray_mesh = new() { Size = new Vector2(10, 1000) };
        private static readonly QuadMesh cube_mesh = new() { Size = new Vector2(100, 100) };

        private static readonly GradientTexture2D
            gradient = SimpleGradientTexture2D.CreateRadial(Colors.Red.Darkened(0.8f), Colors.Red);

        private static readonly ParticlesRid lightrays = ParticlesRid.Create()
            .SetAmount(20)
            .SetMesh(light_ray_mesh.GetRid())
            .SetProcessMaterial(light_ray.GetRid())
            .SetLifetime(5);

        private static readonly ParticlesRid blocks = ParticlesRid.Create()
            .SetAmount(40)
            .SetMesh(cube_mesh.GetRid())
            .SetProcessMaterial(block_material.GetRid())
            .SetLifetime(1000)
            .SetExplosivenessRatio(1);

        private static readonly ParticlesRid dust = ParticlesRid.Create()
            .SetAmount(500)
            .SetMesh(cube_mesh.GetRid())
            .SetProcessMaterial(dust_particle_material.GetRid())
            .SetLifetime(5);

        private static readonly CanvasItemMaterial light_blend_material = new()
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };

        private static readonly ShaderMaterial starry_night_material = new()
        {
            Shader = GD.Load<Shader>("res://Resources/Shaders/StarryNight.gdshader")
        };

        protected override void OnAddStore(EntityStore store)
        {

            GodotTree.Tree.Root.AddChild(staticLayer);

            canvas.SetParent(staticLayer.GetCanvas());
            Vector2 size = new Vector2(10000, 10000);
            Vector2 glowSize = new Vector2(2000, 2000);


            RenderRid.Create(canvas)
                .SetZIndex(-20)


                .SetModulate(Colors.OrangeRed with  { A = 0.2f})
                .SetMaterial(light_blend_material.GetRid())
                .AddParticles(lightrays, CustomDrawExtensions.CIRCULAR_GLOW_TEXTURE.GetRid());


            RenderRid.Create(canvas)
                .SetZIndex(-12)
                .AddMesh(MeshFactory.CreateCrescent(100, 100).GetRid())
                .AddTextureRect( new Rect2(-glowSize / 2, glowSize), CustomDrawExtensions.CIRCULAR_GLOW_TEXTURE.GetRid())
                .SetModulate(Colors.DarkRed with { A = 0.2f});

            RenderRid.Create(canvas)
                .SetZIndex(-11)
                .AddRect(new Rect2(-size / 2, size), Colors.White with  { A = 0.05f});

            RenderRid.Create(canvas)
                .SetZIndex(-9)
                .SetModulate(Colors.Black)
                .AddSetTransform(new Transform2D(0, new Vector2(0, 500)))
                .AddParticles(blocks)
                .AddSetTransform(new Transform2D(0, new Vector2(0, -450)))
                .AddParticles(blocks);


            RenderRid.Create(canvas)
                .SetZIndex(-8)
                .SetModulate(Colors.DarkRed)
                .AddRect(new Rect2(-size / 2, size), Colors.White with { A = 0.05f })
                .AddSetTransform(new Transform2D(0, new Vector2(0, 700)))
                .AddParticles(blocks, gradient.GetRid())
                .AddSetTransform(new Transform2D(0, new Vector2(0, -650)))
                .AddParticles(blocks, gradient.GetRid());


            RenderRid.Create(canvas)
                .SetZIndex(-9)
                .SetModulate(Colors.Black)
                .AddSetTransform(new Transform2D(0, new Vector2(0, 500)))
                .AddParticles(blocks)
                .AddSetTransform(new Transform2D(0, new Vector2(0, -450)))
                .AddParticles(blocks);

            RenderRid.Create(canvas)
                .SetZIndex(-7)

                .SetModulate(new Color(1,0,0,0.02f))
                .SetMaterial(light_blend_material.GetRid())
                .AddParticles(dust, CustomDrawExtensions.CIRCULAR_GLOW_TEXTURE.GetRid());

            s.SetScale(new Vector2(300,300));
            s.EnableSecondShape();
            s.SetShape(SdfShapeMaterial.ShapeType.Box);
            s.WithBooleanOperation(SdfShapeMaterial.BlendMode.Intersection);
            s.SetSecondShape(SdfShapeMaterial.ShapeType.Circle);

            /*RenderRid.Create(canvas)
                .AddParticles(dust);*/

            // Initialize parallax columns
            initializeParallaxColumns();
        }

        private static SdfShapeMaterial s = new();

        // Parallax column configuration
        private readonly List<ParallaxColumnLayer> columnLayers = new();

        private struct ParallaxColumnLayer
        {
            public RenderRid RenderRid;
            public float ParallaxFactor;
            public Vector2 BaseOffset;
            public float StartX;
        }


        private void initializeParallaxColumns()
        {
            // Background layer (farthest - slowest parallax)
            createParallaxLayer(
                zIndex: -19,
                parallaxFactor: 0.02f,
                columnColor: Colors.Red.Darkened(0.9f),
                columnWidth: 60,
                columnHeight: 800,
                columnSpacing: 200,
                columnCount: 15,
                baseOffset: new Vector2(0, -300)
            );

            // Mid-far layer
            createParallaxLayer(
                zIndex: -18,
                parallaxFactor: 0.05f,
                columnColor: Colors.Red.Darkened(0.7f),
                columnWidth: 70,
                columnHeight: 1000,
                columnSpacing: 250,
                columnCount: 20,
                baseOffset: new Vector2(100, -350)
            );

            // Mid layer
            createParallaxLayer(
                zIndex: -17,
                parallaxFactor: 0.1f,
                columnColor: Colors.Red.Darkened(0.5f),
                columnWidth: 80,
                columnHeight: 1500,
                columnSpacing: 300,
                columnCount: 25,
                baseOffset: new Vector2(50, -400)
            );

            // Near layer (fastest parallax)
            createParallaxLayer(
                zIndex: -16,
                parallaxFactor: 0.15f,
                columnColor: Colors.Red.Darkened(0.2f),
                columnWidth: 100,
                columnHeight: 1600,
                columnSpacing: 350,
                columnCount: 30,
                baseOffset: new Vector2(0, -450)
            );
        }

        private void createParallaxLayer(
            int zIndex,
            float parallaxFactor,
            Color columnColor,
            float columnWidth,
            float columnHeight,
            float columnSpacing,
            int columnCount,
            Vector2 baseOffset)
        {

            // Create render layer
            var renderLayer = RenderRid.Create(canvas)
                .SetZIndex(zIndex);

            // Calculate total width and proper centering
            float totalWidth = (columnCount - 1) * columnSpacing;
            float startX = -totalWidth / 2f;

            // Draw columns
            for (int i = 0; i < columnCount; i++)
            {
                float x = startX + i * columnSpacing;
                Vector2 columnPosition = baseOffset + new Vector2(x, 0);

                renderLayer
                    .AddSetTransform(new Transform2D(0, columnPosition))
                    .AddRect(new Rect2(-new Vector2(columnWidth / 2, columnHeight / 2), new Vector2(columnWidth, columnHeight)), columnColor);
            }

            // Add to layers list
            columnLayers.Add(new ParallaxColumnLayer
            {
                RenderRid = renderLayer,
                ParallaxFactor = parallaxFactor,
                BaseOffset = baseOffset,
                StartX = startX
            });
        }

        private Vector2 lastCameraPosition = Vector2.Zero;

        protected override void OnUpdate()
        {
            // Update static layer offset
            staticLayer.Offset = GameServices.Canvas.GetWindow().Size / 2;

            // Get current camera position
            Vector2 currentCameraPosition = GameServices.Canvas.Offset;
            Vector2 cameraMovement = currentCameraPosition - lastCameraPosition;

            // Only update parallax if there's actual movement
            if (cameraMovement.LengthSquared() > 0.001f)
            {
                updateParallaxColumns(cameraMovement);
            }

            // Store current position for next frame
            lastCameraPosition = currentCameraPosition;
        }

        private void updateParallaxColumns(Vector2 cameraMovement)
        {
            foreach (var layer in columnLayers)
            {
                // Calculate parallax movement - farther layers move less
                Vector2 parallaxMovement = cameraMovement * layer.ParallaxFactor;

                // Apply the movement by modifying the transform
                Vector2 newOffset = layer.BaseOffset - parallaxMovement;

                // Update the layer's transform
                layer.RenderRid.SetTransform(new Transform2D(0, newOffset));
            }
        }
    }
}
