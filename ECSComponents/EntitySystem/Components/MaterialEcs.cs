// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    public struct BlockMaterialEcs : IComponent
    {
        public BlockShaderId Shader;

        public int OutlineThickness;

    }
}
