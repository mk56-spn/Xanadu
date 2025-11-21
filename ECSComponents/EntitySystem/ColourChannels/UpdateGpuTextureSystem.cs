// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;

namespace XanaduProject.ECSComponents.EntitySystem.ColourChannels
{
    public class UpdateGpuTextureSystem() : QuerySystem<FloatArrayEcs, ColorArrayEcs, ActiveColourEcs, IndexEcs>
    {

        private readonly IVisualsMaster visualsMaster = DiProvider.Get<IVisualsMaster>();

        private RenderRid r = RenderRid.Create();
        protected override void OnAddStore(EntityStore store)
        {
            r.SetParent(visualsMaster.GameplayerLayerRid);
            base.OnAddStore(store);
            imageTexture = ImageTexture.CreateFromImage(image);
            r.AddTextureRect( new Rect2(0, 1, size, 1), imageTexture.GetRid());
        }

        protected override void OnUpdate()
        {
            updateGpuTexture();
        }

        private const int size = 100;
        private readonly Image image = Image.CreateEmpty(size, 1, false, Image.Format.Rgba8);
        private readonly Color[] colors = new Color[size];
        private ImageTexture imageTexture = null!;
        private readonly byte[] byteColors = new byte[size * 4];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void updateGpuTexture()
        {
            var colorArray = colors;
            for (int i = 0; i < size; i++)
                colorArray[i] = Colors.Purple;




            Query.ForEachEntity((ref FloatArrayEcs _, ref ColorArrayEcs _,
                ref ActiveColourEcs active,ref  IndexEcs e,  Entity _) =>
            {
                colorArray[e.Index] = active.Color;
            });

            // Update byte colors with current color values
            var byteColorsSpan = byteColors.AsSpan();
            for (int i = 0; i < size; i++)
            {
                var color = colorArray[i];
                int byteIndex = i * 4;
                byteColorsSpan[byteIndex] = (byte)(color.R * 255);
                byteColorsSpan[byteIndex + 1] = (byte)(color.G * 255);
                byteColorsSpan[byteIndex + 2] = (byte)(color.B * 255);
                byteColorsSpan[byteIndex + 3] = (byte)(color.A * 255);
            }

            image.SetData(size, 1, false, Image.Format.Rgba8, byteColors);
            imageTexture.Update(image);
            RenderingServer.GlobalShaderParameterSet("colours_texture", imageTexture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void fillRemainingColors(int startIndex)
        {
            if (startIndex >= size) return;

            var fillValue = startIndex > 0 ? colors[startIndex - 1] : Colors.Red;
            for (int i = startIndex; i < size; i++)
                colors[i] = fillValue;

            var byteColorsSpan = byteColors.AsSpan();
            for (int i = 0; i < size; i++)
            {
                var color = colors[i];
                int byteIndex = i * 4;
                byteColorsSpan[byteIndex] = (byte)(color.R * 255);
                byteColorsSpan[byteIndex + 1] = (byte)(color.G * 255);
                byteColorsSpan[byteIndex + 2] = (byte)(color.B * 255);
                byteColorsSpan[byteIndex + 3] = (byte)(color.A * 255);
            }
        }
    }
}
