// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.


using Friflo.EcGui;
using Godot;

namespace XanaduProject.ECSComponents.EcGuiSetup
{
    internal static class TypeDrawers
    {
        internal static void Register()
        {
            EcGui.Setup.RegisterTypeDrawer<Color>(new ColorDrawer());
            EcGui.Setup.RegisterTypeDrawer<Vector2>(new Vector2Drawer());
        }
    }

    internal sealed class Vector2Drawer : TypeDrawer
    {
        public override string[] SortFields => ["X", "Y"];
        public override string[] FormatFields => ["Y", "Y"];
        public override int DefaultWidth => 250;

        public override ItemFlags DrawValue(in DrawValue drawValue)
        {
            if (!drawValue.GetValue<Vector2>(out var value, out var exception))
                return drawValue.DrawException(exception);
            if (EcUtils.InputFloat2(ref value.X, ref value.Y, drawValue, out var flags)) drawValue.SetValue(value);
            return flags;
        }

        public override void Format(MemberFormat format)
        {
            format.GetValue<Vector2>(out var value, out var exception);
            format.Append(value.X, exception);
            format.Append(value.Y, exception);
        }
    }

    internal sealed class ColorDrawer : TypeDrawer
    {
        public override string[] SortFields => ["R", "G", "B", "A"];
        public override string[] FormatFields => ["R", "G", "B", "A"];
        public override int DefaultWidth => 400;

        public override ItemFlags DrawValue(in DrawValue drawValue)
        {
            if (!drawValue.GetValue<Color>(out var value, out var exception)) return drawValue.DrawException(exception);
            if (EcUtils.ColorEdit4(ref value.R, ref value.G, ref value.B, ref value.A, drawValue, out var flags))
                drawValue.SetValue(value);
            return flags;
        }

        public override void Format(MemberFormat format)
        {
            format.GetValue<Color>(out var value, out var exception);
            format.Append(value.R, exception);
            format.Append(value.G, exception);
            format.Append(value.B, exception);
            format.Append(value.A, exception);
        }
    }
}
