// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace XanaduProject.Factories.ShaderFactoryHelpers
{
    public static class FragmentShaders
    {
        public static readonly FragmentPartial TILES =
            new(
                """
                vec2 tile_center = floor(PIXEL_UV / tiles_grid_size) / tiles_grid_size + (0.5 / tiles_grid_size);
                float wave = sin(TIME * tiles_wave_speed + tile_center.x * tiles_wave_frequency + tile_center.y * tiles_wave_frequency);
                wave = (wave + 1.0) / 2.0;
                float opacity = mix(tiles_min_opacity, tiles_max_opacity, wave);
                COLOR.a *= opacity;
                """,
                new List<IUniform>
                {
                    new Uniform<float>("tiles_grid_size", 10.0f),
                    new Uniform<float>("tiles_wave_speed", 1.0f),
                    new Uniform<float>("tiles_wave_frequency", 1.0f),
                    new Uniform<float>("tiles_min_opacity", 0.9f),
                    new Uniform<float>("tiles_max_opacity", 1.0f)
                });
    }
}
