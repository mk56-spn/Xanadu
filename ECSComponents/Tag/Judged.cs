// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.DataStructure;

namespace XanaduProject.ECSComponents.Tag
{
    public struct Judged : IComponent
    {
        public float Deviation;
        public Judgement Judgement;
    };
}
