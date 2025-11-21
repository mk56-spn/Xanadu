// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Friflo.Engine.ECS;

namespace XanaduProject.ECSComponents.EntitySystem.Components
{
    [ComponentKey(null)]
    public readonly struct NameEcs : IIndexedComponent<string>
    {
        public readonly string Name = "Default";
        public string GetIndexedValue() => Name;

        public NameEcs(string name)
        {
            Name = name;
        }
    }
}
