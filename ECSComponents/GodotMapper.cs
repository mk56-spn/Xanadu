// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Json.Fliox.Mapper.Map;
using Godot;

namespace XanaduProject.ECSComponents
{
    public class GodotMapper :  TypeMapper<Color>
    {
        public override bool IsNull(ref Color value)
        {
            throw new NotImplementedException();
        }

        public override void Write(ref Writer writer, Color slot)
        {
            throw new NotImplementedException();
        }

        public override Color Read(ref Reader reader, Color slot, out bool success)
        {
            throw new NotImplementedException();
        }
    }
}
