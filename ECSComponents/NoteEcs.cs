// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Composer;
using XanaduProject.DataStructure;
using XanaduProject.Tools;

namespace XanaduProject.ECSComponents
{
	public struct NoteEcs(NoteType type) : IComponent, IComparable<float>
	{
		public static readonly int RADIUS = 32;

		[Composer("position")] public bool CenterPlayer;

		public NoteType NoteType = type;

		public float TimingPoint;

		public int CompareTo(float other)
		{
			return TimingPoint.CompareTo(other);
		}

		public Rid NoteCanvas;
	}
}
