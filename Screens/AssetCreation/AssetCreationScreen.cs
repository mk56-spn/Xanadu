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
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Scenes.ItemEditor;
using XanaduProject.Screens.AssetCreation.BoneMapping;
using XanaduProject.Screens.AssetCreation.ItemEditor;
using XanaduProject.Screens.AssetCreation.PoseAnimating;
using XanaduProject.Screens.ScreenStructure;
using XanaduProject.Singleton;
using XanaduProject.Utils;
using AnimatedHoverButton = XanaduProject.UiElements.AnimatedHoverButton;

namespace XanaduProject.Screens.AssetCreation
{
    public partial class AssetCreationScreen : MainScreen
    {
        public AssetCreationScreen()
        {
            DisplayName = "Asset Creation";
            setupUiLayout();
            CloseTargetScreen = new MainMenu();
        }

        private void setupUiLayout()
        {
            // Main container for the entire screen
            var mainLayoutContainer = new VBoxContainer();
            AddChild(mainLayoutContainer);
            // Make the main container fill the entire screen
            mainLayoutContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

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


            VBoxContainer itemCreatorTab;

            tabContainer.AddChildren(
                itemCreatorTab = new VBoxContainer()
                {
                    Name = "Item Creator"
                }
                ,
                setupBoneMappingTab(),
                setupAnimationTab()
                 );



            // Existing Item Selection UI elements moved into this tab
            var itemSelectionContainer = new VBoxContainer()
            {
                Name = "ItemSelectionContainer"
            };
            itemCreatorTab.AddChild(itemSelectionContainer);

            var createNewMeshButton = new AnimatedHoverButton("Create new item");
            itemSelectionContainer.AddChild(createNewMeshButton);
            createNewMeshButton.Pressed += () => ScreenManager.RequestChangeScreen(new ItemCreationScreen());

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
                        ScreenManager.RequestChangeScreen((new ItemCreationScreen(selectedItem)));
                };
                itemListContainer.AddChild(itemButton);
            }
            // --- End Item Creator Tab ---



        }

        private VBoxContainer setupBoneMappingTab()
        {

            // --- Bone Mapper Tab ---
            var boneMapperTab = new VBoxContainer();
            boneMapperTab.Name = "Bone Mapper"; // This sets the tab title

            foreach (var skinInfo in SkinIndex.GetAllSkins())
            {
                boneMapperTab.AddChild(new AnimatedHoverButton(skinInfo.Value.Name, pressed: ( )=> ScreenManager.RequestChangeScreen(new ItemBoneMappingScreen(skinInfo))));
            }
            return boneMapperTab;
        }

        private VBoxContainer setupAnimationTab()
        {
            var poseAnimatorTab = new VBoxContainer();
            poseAnimatorTab.Name = "Pose Animator"; // This sets the tab title


            Button button;
            poseAnimatorTab.AddChild( button = new AnimatedHoverButton("+"){ MainColour = Colors.Salmon });

            button.Pressed += () => ScreenManager.RequestChangeScreen(new PoseAnimatingScreen());
            foreach (string animations in AnimationIndex.GetAllAnimationNames())
            {
                var animationButton = new AnimatedHoverButton(animations);
                poseAnimatorTab.AddChild(animationButton);
                animationButton.Pressed += () => ScreenManager.RequestChangeScreen(new PoseAnimatingScreen(animations));
            }

            return poseAnimatorTab;
        }
    }
}
