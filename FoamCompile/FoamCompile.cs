using Assimp;
using Assimp.Configs;
using Foam;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AssMatrix4x4 = Assimp.Matrix4x4;
using NumMatrix4x4 = System.Numerics.Matrix4x4;

namespace FoamCompile {
	unsafe static class Program {
		static AssimpContext Importer;

		static void Main(string[] args) {
			//FoamMesh[] Msh = Load("models/md3/watercan/watercan.md3");

			//string[] InputFiles = Directory.GetFiles("C:/Projekti/Foam/pak0/models", "*.md3", SearchOption.AllDirectories);
			//string[] InputFiles = { "models/fbx/scotty/scotty.fbx" };
			string[] InputFiles = { "models/md5/bob_lamp/bob_lamp_update.md5mesh" };

			foreach (var F in InputFiles) {
				Console.WriteLine("Converting " + F);

				if (Debugger.IsAttached) {
					Convert(F);
				} else {
					try {
						Convert(F);
					} catch (Exception E) {
						Console.WriteLine("Exception: {0}", E.Message);
					}
				}
			}

			//Convert("C:/Projekti/Foam/pak0/models/players/sarge/lower.md3");

			Console.WriteLine("Done!");
			Console.ReadLine();
		}

		static void Convert(string FilePath) {
			string FileName = Path.GetFileNameWithoutExtension(FilePath);
			string RootDir = Path.GetDirectoryName(FilePath);

			FoamMesh[] Msh = Load(FilePath);

			FoamModel Foam = new FoamModel(FileName, FoamFlags.Static, Msh);
			Foam.SaveToFile(Path.Combine(RootDir, FileName + ".foam"));
		}

		static FoamMesh[] Load(string FileName) {
			if (Importer == null) {
				Importer = new AssimpContext();
				Importer.SetConfig(new MD3HandleMultiPartConfig(false));
				Importer.SetConfig(new MD5NoAnimationAutoLoadConfig(true));
				Importer.SetConfig(new VertexBoneWeightLimitConfig(4));
			}

			PostProcessSteps ProcessSteps = PostProcessSteps.Triangulate;
			ProcessSteps |= PostProcessSteps.SplitLargeMeshes;
			ProcessSteps |= PostProcessSteps.OptimizeMeshes;
			ProcessSteps |= PostProcessSteps.LimitBoneWeights;

			Scene Sc = Importer.ImportFile(FileName, ProcessSteps);

			List<FoamMaterial> MaterialList = new List<FoamMaterial>();
			foreach (var Mat in Sc.Materials) {
				FoamMaterial FoamMat = new FoamMaterial(Mat.Name);

				AddTextureIfExists(Mat.HasTextureDiffuse, ref FoamMat, Mat.TextureDiffuse, FoamTextureType.Diffuse);
				AddTextureIfExists(Mat.HasTextureEmissive, ref FoamMat, Mat.TextureEmissive, FoamTextureType.Glow);
				AddTextureIfExists(Mat.HasTextureNormal, ref FoamMat, Mat.TextureNormal, FoamTextureType.Normal);
				AddTextureIfExists(Mat.HasTextureSpecular, ref FoamMat, Mat.TextureSpecular, FoamTextureType.Specular);
				AddTextureIfExists(Mat.HasTextureReflection, ref FoamMat, Mat.TextureReflection, FoamTextureType.Reflection);
				AddTextureIfExists(Mat.HasTextureHeight, ref FoamMat, Mat.TextureHeight, FoamTextureType.Height);
				AddTextureIfExists(Mat.HasTextureLightMap, ref FoamMat, Mat.TextureLightMap, FoamTextureType.LightMap);
				AddTextureIfExists(Mat.HasTextureDisplacement, ref FoamMat, Mat.TextureDisplacement, FoamTextureType.Displacement);
				AddTextureIfExists(Mat.HasTextureAmbient, ref FoamMat, Mat.TextureAmbient, FoamTextureType.Ambient);
				AddTextureIfExists(Mat.HasTextureOpacity, ref FoamMat, Mat.TextureOpacity, FoamTextureType.Opacity);

				MaterialList.Add(FoamMat);
			}

			List<FoamMesh> MeshList = new List<FoamMesh>();
			foreach (var Msh in Sc.Meshes) {
				Vector3D[] Verts = Msh.Vertices.ToArray();
				Vector3D[] UVs = Msh.TextureCoordinateChannels[0].ToArray();

				string MeshName = Msh.Name;
				FoamMaterial Material = MaterialList[Msh.MaterialIndex];

				FoamVertex3[] FoamVertices = new FoamVertex3[Verts.Length];
				for (int i = 0; i < FoamVertices.Length; i++) {
					Vector2 UV = Vector2.Zero;

					if (UVs.Length != 0)
						UV = new Vector2(UVs[i].X, UVs[i].Y);

					FoamVertex3 V = new FoamVertex3(new Vector3(Verts[i].X, Verts[i].Y, Verts[i].Z), UV);
					FoamVertices[i] = V;
				}

				List<ushort> FoamIndices = new List<ushort>();
				foreach (var F in Msh.Faces)
					foreach (var FaceIndex in F.Indices)
						FoamIndices.Add((ushort)FaceIndex);

				FoamBoneInfo[] BoneInfo = null;
				FoamBone[] Bones = null;

				if (Msh.BoneCount != 0) {
					BoneInfo = new FoamBoneInfo[FoamVertices.Length];
					Bone[] OrigBones = Msh.Bones.ToArray();
					Bones = new FoamBone[OrigBones.Length];

					for (int i = 0; i < OrigBones.Length; i++)
						Bones[i] = new FoamBone(OrigBones[i].Name, ConvertMatrix(OrigBones[i].OffsetMatrix));

					for (int i = 0; i < BoneInfo.Length; i++) {
						FindWeightsFor(OrigBones, i, out VertexWeight[] Weights, out int[] VertexBones);

						FoamBoneInfo BInfo = new FoamBoneInfo();
						BInfo.Bone1 = VertexBones[0];
						BInfo.Bone2 = VertexBones[1];
						BInfo.Bone3 = VertexBones[2];
						BInfo.Bone4 = VertexBones[3];

						BInfo.Weight1 = Weights[0].Weight;
						BInfo.Weight2 = Weights[1].Weight;
						BInfo.Weight3 = Weights[2].Weight;
						BInfo.Weight4 = Weights[3].Weight;

						BoneInfo[i] = BInfo;
					}
				}

				MeshList.Add(new FoamMesh(FoamVertices, FoamIndices?.ToArray() ?? null, BoneInfo, Bones, MeshName, Material));
			}

			return MeshList.ToArray();
		}

		static void FindWeightsFor(Bone[] Bones, int VertexID, out VertexWeight[] Weights, out int[] VertexBones) {
			List<VertexWeight> WeightsList = new List<VertexWeight>();
			List<int> VertexBonesList = new List<int>();

			for (int i = 0; i < Bones.Length; i++)
				foreach (var W in Bones[i].VertexWeights)
					if (W.VertexID == VertexID) {
						WeightsList.Add(W);
						VertexBonesList.Add(i);
					}

			while (WeightsList.Count < 4) {
				WeightsList.Add(new VertexWeight(0, 0));
				VertexBonesList.Add(0);
			}

			VertexBones = VertexBonesList.ToArray();
			Weights = WeightsList.ToArray();
		}

		static NumMatrix4x4 ConvertMatrix(AssMatrix4x4 Mat) {
			return *(NumMatrix4x4*)&Mat;
		}

		static void AddTextureIfExists(bool Exists, ref FoamMaterial FoamMat, TextureSlot Texture, FoamTextureType TexType) {
			if (Exists) {
				FoamMat.AddTexture(new FoamTexture(Texture.FilePath, TexType));
			}
		}
	}
}
