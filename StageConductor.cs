// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Audio;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.GameDependencies;
using PhysicsMaster = XanaduProject.Stage.Masters.Physics.PhysicsMaster;
using UiMaster = XanaduProject.Stage.Masters.UI.UiMaster;
using VisualsMaster = XanaduProject.Stage.Masters.Visual.VisualsMaster;

namespace XanaduProject
{
	public partial class StageConductor : Node2D, IStageConductor
	{
		private readonly EntityStore entityStore;
		public Root Root = null!;
		public Clock Clock { get; }

		public StageConductor(TrackInfo trackInfo, EntityStore entityStore)
		{
			this.entityStore = entityStore;
			entityStore.EventRecorder.Enabled = true;
			Clock = new Clock(trackInfo);

			DiProvider.Register(collection => collection.AddSingleton<IClock>(Clock));

			AddChild(Clock);
			createGlobalNodes();


			var staticBody2D = new StaticBody2D { Position = new Vector2(0, 1000) };
			staticBody2D.AddChild(new CollisionShape2D { Shape = new WorldBoundaryShape2D() });

			AddChild(staticBody2D);

		}

		private void createGlobalNodes()
		{
			var window = this;

			var uiMaster = new UiMaster();
			window.AddChild(uiMaster);


			var physicsMaster = new PhysicsMaster();
			window.AddChild(physicsMaster);
			var playerCharacter = new PlayerCharacter();
			window.AddChild(playerCharacter);



			var visualsMaster = new VisualsMaster();
			window.AddChild(visualsMaster);

			DiProvider.Configure(services =>
			{
				services.AddSingleton<IPhysicsMaster>(physicsMaster);
				services.AddSingleton<IUiMaster>(uiMaster);
				services.AddSingleton<IPlayerCharacter>(playerCharacter);
				services.AddSingleton<IVisualsMaster>(visualsMaster);
				services.AddSingleton(entityStore);
			});

			Root = new Root(entityStore);

		}

		public override void _Process(double delta)
		{
			Root.Update(new UpdateTick());
            entityStore.EventRecorder.ClearEvents();
		}

		public void DamageTaken()
		{
		}
	}
}
