// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text;

namespace XanaduProject.Factories.ShaderFactoryHelpers
{

        public abstract class UniformHint
        {
            public abstract string ToShaderString();
        }

        internal class SimpleHint : UniformHint
        {
            private readonly string _hint;
            internal SimpleHint(string hint)
            {
                _hint = hint;
            }
            public override string ToShaderString() => _hint;
        }

        internal class RangeHint : UniformHint
        {
            private readonly float _min;
            private readonly float _max;
            private readonly float? _step;

            internal RangeHint(float min, float max, float? step)
            {
                _min = min;
                _max = max;
                _step = step;
            }

            public override string ToShaderString()
            {
                var builder = new StringBuilder();
                builder.Append($"hint_range({_min.ToString(CultureInfo.InvariantCulture)}, {_max.ToString(CultureInfo.InvariantCulture)}");
                if (_step.HasValue)
                {
                    builder.Append($", {_step.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                builder.Append(")");
                return builder.ToString();
            }
        }

}
