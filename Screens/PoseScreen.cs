// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.Skeleton;

namespace XanaduProject.Screens
{
    public abstract partial class PoseScreen : StoreScreen
    {
        protected PoseScreen()
        {
            Container control = new Container();
            Root.Add(new ItemDisplaySystem());
            Root.Add(new BoneGroup("bones"));


            DiProvider.Register(c=>c.AddKeyedSingleton(UiControlKeys.PoseViewer, control));
        }
    }
}
