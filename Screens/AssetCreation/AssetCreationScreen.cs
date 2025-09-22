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
using XanaduProject.GameDependencies;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.IO.Indexes;
using XanaduProject.IO;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Scenes.ItemEditor.BoneMapping;
using Logger = XanaduProject.Singleton.Logger;

namespace XanaduProject.Scenes
{
    public partial class AssetCreationScreen : MainScreen
    {
        private readonly EntityStore entityStore = new()
        {
            JobRunner = new ParallelJobRunner(10, "n")
        };
        private readonly SystemRoot simulationRoot;

        public AssetCreationScreen()
        {

            DiProvider.Register(c =>
            {
                c.AddSingleton(entityStore);
            });
            simulationRoot = new SystemRoot(entityStore)
            {
                new BoneTransformSystem(),
                new IkSolverSystem(),
                new BoneRenderingSystem(),
                new EcsDebugSystem(),
            };


            setupUiLayout();

            Logger.AddLog(LogCategory.Animation, "Ik rigging screen with mesh editor initialized");
        }


        private void setupUiLayout()
        {
            // Main container for the entire screen
            var mainLayoutContainer = new VBoxContainer();
            AddChild(mainLayoutContainer);
            // Make the main container fill the entire screen
            mainLayoutContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            // Top Bar
            var topBarContainer = new HBoxContainer();
            topBarContainer.CustomMinimumSize = new Vector2(0, 50); // Set a minimum height for the top bar
            topBarContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            topBarContainer.Alignment = BoxContainer.AlignmentMode.Center; // Center content horizontally
            mainLayoutContainer.AddChild(topBarContainer);

            var titleLabel = new Label();
            titleLabel.Text = "Asset Editor";
            // Assuming a theme is available or default font sizes can be overridden
            titleLabel.AddThemeFontSizeOverride("font_size", 24);
            topBarContainer.AddChild(titleLabel);

            // Margin Container for the Tab Container
            var marginContainer = new MarginContainer();
            marginContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            marginContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            // Set margins (e.g., 10 pixels on all sides)
            marginContainer.AddThemeConstantOverride("margin_left", 20);
            marginContainer.AddThemeConstantOverride("margin_top", 20);
            marginContainer.AddThemeConstantOverride("margin_right", 20);
            marginContainer.AddThemeConstantOverride("margin_bottom", 20);
            mainLayoutContainer.AddChild(marginContainer);

            // Tab Container
            var tabContainer = new TabContainer();
            tabContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tabContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            marginContainer.AddChild(tabContainer);

            // --- Item Creator Tab ---
            var itemCreatorTab = new VBoxContainer();
            itemCreatorTab.Name = "Item Creator"; // This sets the tab title
            tabContainer.AddChild(itemCreatorTab);

            // Existing Item Selection UI elements moved into this tab
            var itemSelectionContainer = new VBoxContainer();
            itemSelectionContainer.Name = "ItemSelectionContainer";
            itemCreatorTab.AddChild(itemSelectionContainer);

            var createNewMeshButton = new AnimatedHoverButton("Create new item");
            itemSelectionContainer.AddChild(createNewMeshButton);
            createNewMeshButton.Pressed += () => ScreenManager.ChangeSubScreen(new ItemCreationSubScreen());

            var scrollContainer = new ScrollContainer() { CustomMinimumSize = new Vector2(500, 100)};
            scrollContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            itemSelectionContainer.AddChild(scrollContainer);

            var itemListContainer = new VBoxContainer();
            itemListContainer.Name = "ItemListContainer";
            scrollContainer.AddChild(itemListContainer);

            // Populate item list
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
                        ScreenManager.ChangeSubScreen(new ItemCreationSubScreen(selectedItem));
                };
                itemListContainer.AddChild(itemButton);
            }
            // --- End Item Creator Tab ---


            // --- Bone Mapper Tab ---
            var boneMapperTab = new VBoxContainer();
            boneMapperTab.Name = "Bone Mapper"; // This sets the tab title
            tabContainer.AddChild(boneMapperTab);

            // Existing Item Bone Mapping button moved into this tab
            var itemBoneMappingButton = new AnimatedHoverButton("Item Bone Mapping", 15);
            boneMapperTab.AddChild(itemBoneMappingButton);
            itemBoneMappingButton.Pressed += () => ScreenManager.RequestChangeScreen(new ItemBoneMappingScreen(simulationRoot));
            // Additional UI for bone mapping can be added here later

            // --- Skeleton Animator Tab ---
            var poseAnimatorTab = new VBoxContainer();
            poseAnimatorTab.Name = "Skeleton Animator"; // This sets the tab title
            tabContainer.AddChild(poseAnimatorTab);

            // Existing Skeleton Animating button moved into this tab
            var poseButton = new AnimatedHoverButton("Skeleton Animating", 15);
            poseAnimatorTab.AddChild(poseButton);
            poseButton.Pressed += () => ScreenManager.ChangeSubScreen(new PoseAnimatingSubScreen());
            // Additional UI for pose animating can be added here later
        }

        public override void _Process(double delta)
        {
            simulationRoot.Update(default);
        }
    }
}
