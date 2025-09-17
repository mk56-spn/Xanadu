// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using Xanadu.Singletons;
using XanaduProject.Buttons;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.EntitySystem.BoneSystems;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Scenes.MeshEditor;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.IO.Indexes; // Added for ItemIndex
using XanaduProject.IO; // Added for Item
using Logger = XanaduProject.Singleton.Logger;

namespace XanaduProject.Scenes
{
    public partial class IkRiggingScreen : MainScreen
    {
        private readonly EntityStore entityStore = new()
        {
            JobRunner = new ParallelJobRunner(10, "n")
        };
        private readonly SystemRoot simulationRoot;

        public IkRiggingScreen()
        {
            DiProvider.Register(c =>
            {
                c.AddSingleton(entityStore);
            });
            PoseBuilder.BuildPose();


            simulationRoot = new SystemRoot(entityStore)
            {
                new BoneTransformSystem(),
                new IkSolverSystem(),
                new BoneRenderingSystem(),
                new EcsDebugSystem(),
            };

            entityStore.Query<RootEcs>().ForEachEntity((ref RootEcs rootEcs, Entity entity) =>
            {
                rootEcs.Canvas.SetTransform(new Transform2D(0, new Vector2(200, 200)));
                rootEcs.Canvas.SetParent(GetCanvasItem()).SetZIndex(100);
            });

            setupUiLayout();

            Logger.AddLog(LogCategory.Animation, "Ik rigging screen with mesh editor initialized");
        }


        private void setupUiLayout()
        {
            var menuContainer = new VBoxContainer();
            AddChild(menuContainer);

            var poseButton = new AnimatedHoverButton("Pose Animating", 15);
            menuContainer.AddChild(poseButton);
            poseButton.Pressed += () => ScreenManager.ChangeSubScreen(new PoseAnimatingSubScreen());

            // --- Item Selection UI ---
            var itemSelectionContainer = new VBoxContainer();
            itemSelectionContainer.Name = "ItemSelectionContainer";
            menuContainer.AddChild(itemSelectionContainer);

            var createNewMeshButton = new Button { Text = "Create New Mesh" };
            itemSelectionContainer.AddChild(createNewMeshButton);
            createNewMeshButton.Pressed += () => ScreenManager.ChangeSubScreen(new MeshCreationSubScreen(null)); // Pass null for new item

            var scrollContainer = new ScrollContainer() { CustomMinimumSize = new Vector2(500, 100)};
            scrollContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            itemSelectionContainer.AddChild(scrollContainer);

            var itemListContainer = new VBoxContainer();
            itemListContainer.Name = "ItemListContainer";
            scrollContainer.AddChild(itemListContainer);

            AddChild(itemListContainer);


            foreach (var itemInfoEntry in ItemIndex.GetAllItemInfos())
            {
                Logger.AddLog(LogCategory.General, $"ItemIndex: Loaded item '{itemInfoEntry.Key}' from '{itemInfoEntry.Value.FilePath}'");
                var itemInfo = itemInfoEntry.Value;
                var itemButton = new Button
                {
                    Text = itemInfo.Name,
                    TooltipText = itemInfo.Description
                };
                itemButton.Pressed += () =>
                {
                    Item? selectedItem = ItemIndex.GetItem(itemInfo.Name);
                    if (selectedItem != null)
                    {
                        ScreenManager.ChangeSubScreen(new MeshCreationSubScreen(selectedItem));
                    }
                    else
                    {
                        GD.PrintErr($"Failed to load item: {itemInfo.Name}");
                    }
                };
                itemListContainer.AddChild(itemButton);

            }
            itemListContainer.AddChild(new AnimatedHoverButton("lmo"));

            // --- End Item Selection UI ---
        }

        public override void _Process(double delta)
        {
            simulationRoot.Update(default);
        }
    }
}
