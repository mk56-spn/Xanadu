// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public class LoopingSystem : QuerySystem<AnimationInfo>
    {
        private int pingPongDirection = 1;

        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref AnimationInfo info, Entity _) =>
            {
                if (info.AnimationActive != AnimationState.Playing) return;

                float step = Tick.deltaTime;

                switch (info.Mode)
                {
                    case AnimationMode.Loop:
                        info.AnimationPos += step;
                        if (info.AnimationPos > info.Duration)
                        {
                            info.AnimationPos = 0;
                        }
                        break;
                    case AnimationMode.Once:
                        info.AnimationPos += step;
                        if (info.AnimationPos > info.Duration)
                        {
                            info.AnimationPos = info.Duration;
                            info.AnimationActive = AnimationState.Active;
                        }
                        break;
                    case AnimationMode.PingPong:
                        info.AnimationPos += step * pingPongDirection;
                        if (info.AnimationPos > info.Duration)
                        {
                            info.AnimationPos = info.Duration;
                            pingPongDirection = -1;
                        }
                        else if (info.AnimationPos < 0)
                        {
                            info.AnimationPos = 0;
                            pingPongDirection = 1;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            } ));

        }
    }
}
