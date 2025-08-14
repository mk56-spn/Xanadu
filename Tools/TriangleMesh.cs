// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using Godot.Collections;

namespace XanaduProject.Tools
{
        [Tool]
        public partial class TriangleMesh : ArrayMesh
    {
        [Export]
        public int Triangles = 9;

        public TriangleMesh()
        {
            Array surfaceArray = new Array();
            surfaceArray.Resize(3000);

            AddSurfaceFromArrays(PrimitiveType.Triangles, surfaceArray);;
        }
    }
}
