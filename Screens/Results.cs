// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens
{
	public partial class Results : Control
	{
		private Results() { }

		// ReSharper disable once InconsistentNaming
		[Export] public Label UR = null!;
		[Export] private Label accuracy = null!;

		public static double StandardDeviation(double[] values)
		{
			double avg = values.Average();
			return 10 * Math.Sqrt(values.Average(v=>Math.Pow(v-avg,2)));
		}

		private static double computeAccuracy(Judgement[] values)
		{
			return 100F / values.Length * values.Sum(ju => ju switch
			{
				Judgement.FlawlessP or Judgement.Flawless => 1,
				Judgement.Clean => 0.8f,
				Judgement.Fair => 0.7f,
				Judgement.Deficient => 0.3f,
				Judgement.Terrible or Judgement.Miss => 0,
				_ => throw new ArgumentOutOfRangeException()
			});
		}

		public static Results Create(double[] ur, Judgement[] judgements)
		{
			var v =  GD.Load<PackedScene>("uid://wmofwc71b5x1").Instantiate<Results>();
			v.accuracy.Text = computeAccuracy(judgements).ToString("0.00") + "%";
			v.UR.Text = StandardDeviation(ur).ToString("0.00") + "UR";

			return v;
		}
	}
}
