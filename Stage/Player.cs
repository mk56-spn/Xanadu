// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.Stage
{
    public partial class Player : Screen
    {
        public EntityStore EntityStore { get; }

        public TrackInfo TrackInfo { get; }
        public StageConductor StageConductor;

        public Player(SerializableStage serializableStage, TrackInfo trackInfo)
        {
            TrackInfo = trackInfo;
            AddChild(StageConductor = new StageConductor(new TrackInfo
            {
                SongTitle = "Heavens's Fall",
                Track = "res://Resources/Helblinde - Heaven_s Fall.ogg",
                TimingPoints = [(0, 200)]
            },EntityStore = serializableStage.EntityStore));

            if (this is not Masters.Composer.Composer)
            {
                StageConductor.AddChild(new PlayerCamera());

                AddChild(new Pause(this));
                Ready += () =>
                {
                    StageConductor.Clock.Restart();
                    StageConductor.Clock.Resume();
                };
            }
        }
        public Player(EntityStore entityStore, TrackInfo trackInfo)
        {
            TrackInfo = trackInfo;
            AddChild(StageConductor = new StageConductor(new TrackInfo
            {
                SongTitle = "Heavens's Fall",
                Track = "res://Resources/Helblinde - Heaven_s Fall.ogg",
                TimingPoints = [(0, 200)]
            },EntityStore = entityStore));
        }
    }
}
