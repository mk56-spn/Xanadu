// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using JetBrains.Annotations;
using static Godot.Colors;

namespace XanaduProject.ECSComponents
{
	[ComponentKey(null)]
	public struct ParticlesEcs : IComponent
	{
		[UsedImplicitly] private static readonly ParticleProcessMaterial directional_material =
			GD.Load<ParticleProcessMaterial>("uid://c4kcv6dwvgawm");

		[UsedImplicitly] private static readonly ParticleProcessMaterial material =
			GD.Load<ParticleProcessMaterial>("uid://b2k2vileav4cl");

		[UsedImplicitly] private static readonly ArrayMesh circle_mesh = GD.Load<ArrayMesh>("uid://bsd375l20sim8");
		[UsedImplicitly] private static readonly ArrayMesh arrow_mesh = GD.Load<ArrayMesh>("uid://devv00es6ij2q");


		public Rid Particles;

		private readonly GradientTexture2D gradientTexture2D = new()
		{
			Width = 10,
			Height = 10,
			Gradient = new Gradient
			{
				Colors = [White],
				Offsets = [1]
			}
		};

		private ParticlesEcs(ArrayMesh mesh, ParticleProcessMaterial material, int amount, float lifetime)
		{

		}

		public static ParticlesEcs DirectionalParticleCreate()
		{
			return new ParticlesEcs(arrow_mesh, directional_material, 2, 1);
		}

		public static ParticlesEcs NormalParticleCreate()
		{
			return new ParticlesEcs(circle_mesh, material, 30, 3);
		}
	}
}
