// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Stage.Masters.Composer
{
    public interface IComposerVisuals
    {
        public void TopBarAddWidget(Control control);

        public void EntityEditTabAdd(Control control, string header);

        public void LeftBarAddWidget(Control control);

        public void AddTabToMain(Container container);


        public void AddTabToLeft(Control control);

        public void AddToFloatingBar(Control control);


        public CanvasLayer UiLayer { get; }
    }
}
