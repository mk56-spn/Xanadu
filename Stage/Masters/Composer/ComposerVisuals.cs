// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.GameDependencies;
using XanaduProject.Serialization;

namespace XanaduProject.Stage.Masters.Composer
{
	public partial class ComposerVisuals : CanvasLayer, IComposerVisuals
	{
		private Slider trackPos = new HSlider();
		private VisualsLayout layout = new();

		public ComposerVisuals()
		{
			UiLayer = this;
			AddChild(new PanningCamera());
			AddChild(layout);
		}

		public void TopBarAddWidget(Control control) => layout.TopBar.AddChild(control);
		public void EntityEditTabAdd(Control control, string header)
		{
			layout.EntityEdit.AddChild(new VisibilityLabel(control) { Text = header });
			layout.EntityEdit.AddChild(control);
		}

		public void LeftBarAddWidget(Control control)
		{
			layout.LeftBar.AddChild(control);
		}

		public void AddTabToMain(Container container)
		{
			layout.MainTabs.AddChild(container);
		}

		public void AddTabToLeft(Control container)
		{
			layout.LeftBottomContainer.AddChild(container);
		}

		public void AddToFloatingBar(Control control)
		{
			layout.FloatingBar.AddChild(control);
		}

		public CanvasLayer UiLayer { get; }

		private partial class VisualsLayout : VBoxContainer
		{
			public HBoxContainer TopBar = new() { CustomMinimumSize = new Vector2(0,50) };
			public readonly TabContainer MainTabs = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill};
			public readonly HBoxContainer LeftBottomContainer = new() ;
			public readonly VBoxContainer FloatingBar = new()
			{
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				SizeFlagsVertical = SizeFlags.ShrinkBegin
			};

			public VBoxContainer LeftBar = new();
			public VBoxContainer EntityEdit = new();

			public VisualsLayout()
			{
				SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				CustomMinimumSize = new Vector2(1000, 1000);
				topBarCreate();

				center();
				bottomBar();
			}
			private void topBarCreate()
			{
				AddChild(panelWrapper(TopBar));
				Button b = new Button {CustomMinimumSize = new Vector2(30,30)};
				TopBar.AddChild(b);

				b.Pressed += () =>
				{
					StageSerializer.Serialize(DiProvider.Get<EntityStore>(), "level1");
				};
			}


			private void center()
			{
				PanelContainer container = new PanelContainer
				{
					SelfModulate = Colors.Transparent,
					MouseFilter = MouseFilterEnum.Pass,
					SizeFlagsVertical = SizeFlags.ExpandFill
				};

				AddChild(container);
				var leftPanel = panelWrapper(LeftBar);
				leftPanel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
				leftPanel.SizeFlagsVertical = SizeFlags.ShrinkCenter;

				container.AddChild(leftPanel);
				AddChild(FloatingBar);

				var entityPanel = panelWrapper(EntityEdit);
				container.AddChild(entityPanel);
				entityPanel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
				entityPanel.Position -= new Vector2(30, 50);
			}
			private void bottomBar()
			{
				HBoxContainer container = new HBoxContainer
				{
					CustomMinimumSize = new Vector2(0,100)
				};
				AddChild(container);
				container.AddChild(LeftBottomContainer);
				container.AddChild(MainTabs);

				Button b = new Button { Text = "PLAY", CustomMinimumSize = new Vector2(200,0)};
				b.Pressed += () =>
				{
					var composer = DiProvider.Get<IComposer>();
					composer.ScreenManager.RequestChangeScreen(new Player(composer.EntityStore, composer.TrackInfo));
				};
				container.AddChild(b);
			}

			private static Control panelWrapper(Control control)
			{
				PanelContainer panel = new PanelContainer();
				panel.AddChild(control);
				return panel;
			}
		}
	}
}
