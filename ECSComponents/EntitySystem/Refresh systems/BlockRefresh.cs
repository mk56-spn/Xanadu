// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
	public class BlockRefresh : BaseRefreshSystem<BlockEcs>
	{
		protected override void OnUpdate()
		{
			foreach (var (elements, blocks, entities) in Query.Chunks)
				for (int n = 0; n < entities.Length; n++)
						PhysicsServer2D.BodySetShapeTransform(blocks[n].Body, 0, elements[n].Transform);
		}
	}
}
