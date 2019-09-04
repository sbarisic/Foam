using Foam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoamCompile {
	enum MD3_Sex {
		Male,
		Female
	}

	public struct MD3_Animation {
		public string Name;
		public int FirstFrame;
		public int NumFrames;
		public int LoopingFrames;
		public int FPS;

		public MD3_Animation(string Name, int FirstFrame, int NumFrames, int LoopingFrames, int FPS) {
			this.Name = Name;
			this.FirstFrame = FirstFrame;
			this.NumFrames = NumFrames;
			this.LoopingFrames = LoopingFrames;
			this.FPS = FPS;
		}

		public override string ToString() {
			return Name;
		}
	}

	class MD3_AnimationCfg {
		public bool Exists;

		public MD3_Sex Sex;
		public string Footsteps;
		public MD3_Animation[] Animations;

		public IEnumerable<MD3_Animation> GetAnimations(bool IsUpper) {
			foreach (var A in Animations) {
				if (!IsUpper && A.Name.Contains("TORSO"))
					continue;

				if (IsUpper && A.Name.Contains("LEGS"))
					continue;

				yield return A;
			}
		}

		public MD3_AnimationCfg(string FileName) {
			Exists = false;
			Footsteps = "default";
			Sex = MD3_Sex.Male;

			string[] AnimationNames = { "BOTH_DEATH1", "BOTH_DEAD1", "BOTH_DEATH2", "BOTH_DEAD2",
				"BOTH_DEATH3", "BOTH_DEAD3", "TORSO_GESTURE", "TORSO_ATTACK", "TORSO_ATTACK2", "TORSO_DROP",
				"TORSO_RAISE", "TORSO_STAND", "TORSO_STAND2", "LEGS_WALKCR", "LEGS_WALK", "LEGS_RUN",
				"LEGS_BACK", "LEGS_SWIM", "LEGS_JUMP", "LEGS_LAND", "LEGS_JUMPB", "LEGS_LANDB", "LEGS_IDLE",
				"LEGS_IDLECR", "LEGS_TURN" };
			int CurAnimation = 0;
			int LegDelta = 0;

			if (File.Exists(FileName)) {
				Exists = true;
				string[] Lines = File.ReadAllText(FileName).Replace("\r", "").Replace("\t", " ").Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToArray();

				foreach (var L in Lines) {
					string Line = L.Trim().ToLower();
					if (Line.StartsWith("//"))
						continue;

					string[] LineTokens = Line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

					if (Line.StartsWith("sex")) {
						if (LineTokens[1] == "m")
							Sex = MD3_Sex.Male;
						else
							Sex = MD3_Sex.Female;
					} else if (Line.StartsWith("footsteps"))
						Footsteps = LineTokens[1];
					else if (char.IsNumber(LineTokens[0][0])) {
						// LEGS_WALKCR - 13
						// TORSO_GESTURE - 6

						if (CurAnimation == 13)
							LegDelta = int.Parse(LineTokens[0]) - Animations[6].FirstFrame;

						Utils.Append(ref Animations, new MD3_Animation(AnimationNames[CurAnimation++], int.Parse(LineTokens[0]) - LegDelta, int.Parse(LineTokens[1]), int.Parse(LineTokens[2]), int.Parse(LineTokens[3])));
					}
				}
			}
		}
	}
}