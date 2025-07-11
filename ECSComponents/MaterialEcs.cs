// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Rendering;

namespace XanaduProject.ECSComponents
{
    public struct MaterialEcs : IComponent
    {
        public BlockMaterialId Shader;
    }
}
