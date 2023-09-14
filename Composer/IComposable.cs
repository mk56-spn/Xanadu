// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using XanaduProject.Composer.Selectables;

namespace XanaduProject.Composer
{
    public interface IComposable
    {
        /// <summary>
        /// Called in order to add the corresponding <see cref="Selectables.Selectable"/> to this <see cref="IComposable"/> node.
        /// </summary>
        public Selectable Selectable { get; }
    }
}
