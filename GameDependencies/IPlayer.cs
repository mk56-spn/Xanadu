// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using XanaduProject.Screens;

namespace XanaduProject.GameDependencies
{
    public interface IPlayer
    {
        protected ScreenManager Manager { get; set; }
        public bool IsComposer { get; }
        public void RequestResults();
    }
}
