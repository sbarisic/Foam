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
using AssQuaternion = Assimp.Quaternion;
using NumMatrix4x4 = System.Numerics.Matrix4x4;

namespace FoamCompile {
	unsafe static class Program {
		static AssimpContext Importer;

		static void Main(string[] args) {
			//FoamMesh[] Msh = Load("models/md3/watercan/watercan.md3");

			//string[] InputFiles = Directory.GetFiles("C:/Projekti/Foam/pak0/models", "*.md3", SearchOption.AllDirectories);
			string[] InputFiles = { "models/md5/bob_lamp/bob_lamp_update.md5mesh" };
			//string[] InputFiles = { "models/blend/gladiator/Gladiator.blend" };
			//string[] InputFiles = { "models/fbx/anitest/ThirdPersonRun.FBX", "models/fbx/anitest/mainani/SK_Mannequin.FBX" };
			//string[] InputFiles = { "models/md5/skeleton/skeleton.md5mesh" };
			//string[] InputFiles = { "models/fbx/scotty/scotty.fbx" };

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

			FoamModel Foam = Load(FilePath);
			Foam.SaveToFile(Path.Combine(RootDir, FileName + ".foam"));
		}

		static FoamModel Load(string FileName) {
			if (Importer == null) {
				Importer = new AssimpContext();
				Importer.SetConfig(new MD3HandleMultiPartConfig(false));
				//Importer.SetConfig(new MD5NoAnimationAutoLoadConfig(true));
				Importer.SetConfig(new VertexBoneWeightLimitConfig(4));
			}

			PostProcessSteps ProcessSteps = PostProcessSteps.Triangulate;
			ProcessSteps |= PostProcessSteps.SplitLargeMeshes;
			ProcessSteps |= PostProcessSteps.OptimizeMeshes;
			ProcessSteps |= PostProcessSteps.LimitBoneWeights;
			ProcessSteps |= PostProcessSteps.JoinIdenticalVertices;
			ProcessSteps |= PostProcessSteps.ImproveCacheLocality;
			ProcessSteps |= PostProcessSteps.GenerateNormals;
			ProcessSteps |= PostProcessSteps.GenerateUVCoords;


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

			FoamBone[] Bones = new FoamBone[0];
			List<FoamMesh> MeshList = new List<FoamMesh>();

			foreach (var Msh in Sc.Meshes) {
				Vector3D[] Verts = Msh.Vertices.ToArray();
				Vector3D[] UVs1 = Msh.TextureCoordinateChannels[0].ToArray();
				Vector3D[] UVs2 = Msh.TextureCoordinateChannels[1].ToArray();
				Vector3D[] Normals = Msh.Normals.ToArray();
				Vector3D[] Tangents = Msh.Tangents.ToArray();
				Color4D[] Colors = Msh.VertexColorChannels[0].ToArray();

				string MeshName = Msh.Name;
				FoamMaterial Material = MaterialList[Msh.MaterialIndex];

				FoamVertex3[] FoamVertices = new FoamVertex3[Verts.Length];
				for (int i = 0; i < FoamVertices.Length; i++) {
					Vector2 UV1 = UVs1.Length != 0 ? new Vector2(UVs1[i].X, UVs1[i].Y) : Vector2.Zero;
					Vector2 UV2 = UVs2.Length != 0 ? new Vector2(UVs2[i].X, UVs2[i].Y) : Vector2.Zero;
					Vector3 Normal = Normals.Length != 0 ? new Vector3(Normals[i].X, Normals[i].Y, Normals[i].Z) : Vector3.Zero;
					Vector3 Tangent = Tangents.Length != 0 ? new Vector3(Tangents[i].X, Tangents[i].Y, Tangents[i].Z) : Vector3.Zero;
					FoamColor Color = Colors.Length != 0 ? new FoamColor(Colors[i].R, Colors[i].G, Colors[i].B, Colors[i].A) : FoamColor.White;

					FoamVertex3 V = new FoamVertex3(new Vector3(Verts[i].X, Verts[i].Y, Verts[i].Z), UV1, UV2, Normal, Tangent, Color);
					FoamVertices[i] = V;
				}

				bool CalculateTangents = Tangents.Length == 0 && UVs1.Length != 0;
				//bool CalculateNormals = Normals.Length == 0;

				List<ushort> FoamIndices = new List<ushort>();
				foreach (var F in Msh.Faces) {
					ushort IndexA = (ushort)F.Indices[0];
					ushort IndexB = (ushort)F.Indices[1];
					ushort IndexC = (ushort)F.Indices[2];

					if (CalculateTangents) {
						FoamVertex3 V0 = FoamVertices[IndexA];
						FoamVertex3 V1 = FoamVertices[IndexB];
						FoamVertex3 V2 = FoamVertices[IndexC];

						Vector3 DeltaPos1 = V1.Position - V0.Position;
						Vector3 DeltaPos2 = V2.Position - V0.Position;

						//if (CalculateTangents) {
						Vector2 DeltaUV1 = V1.UV - V0.UV;
						Vector2 DeltaUV2 = V2.UV - V0.UV;

						Vector3 Tangent = (DeltaPos1 * DeltaUV2.Y - DeltaPos2 * DeltaUV1.Y) * (1.0f / (DeltaUV1.X * DeltaUV2.Y - DeltaUV1.Y * DeltaUV2.X));
						FoamVertices[IndexA].Tangent = FoamVertices[IndexB].Tangent = FoamVertices[IndexC].Tangent = Tangent;
						/*}

						if (CalculateNormals)
							FoamVertices[IndexA].Normal = FoamVertices[IndexB].Normal = FoamVertices[IndexC].Normal = Vector3.Normalize(Vector3.Cross(DeltaPos1, DeltaPos2));*/
					}

					FoamIndices.AddRange(F.Indices.Select(I => (ushort)I));
				}

				FoamBoneInfo[] BoneInfo = null;

				if (Msh.BoneCount != 0) {
					BoneInfo = new FoamBoneInfo[FoamVertices.Length];
					Bone[] OrigBones = Msh.Bones.ToArray();

					// Convert bones
					for (int i = 0; i < OrigBones.Length; i++) {
						if (!ContainsBoneNamed(Bones, OrigBones[i].Name)) {
							Utils.Append(ref Bones, new FoamBone(OrigBones[i].Name, -1, ConvertMatrix(OrigBones[i].OffsetMatrix)));
						}
					}

					// Convert vertex bone information
					for (int i = 0; i < FoamVertices.Length; i++)
						BoneInfo[i] = FindWeightsFor(OrigBones, Bones, i);
				}

				MeshList.Add(new FoamMesh(FoamVertices, FoamIndices?.ToArray() ?? null, BoneInfo, MeshName, Material));
			}

			if (Bones.Length > 0) {
				Node[] NodeHierarchy = Flatten(Sc.RootNode);
				//Node SceneRootNode = Sc.RootNode;
				Node RootNode = FindRoot(FindNode(NodeHierarchy, Bones[0].Name), Bones);
				Utils.Prepend(ref Bones, new FoamBone(RootNode.Name, -1, ConvertMatrix(RootNode.Transform)));

				/*Node RootNodeTest = RootNode;
				while (RootNodeTest.Parent != null) {
					Bones[0].BindMatrix = Bones[0].BindMatrix * ConvertMatrix(RootNodeTest.Transform);
					RootNodeTest = RootNodeTest.Parent;
				}*/


				/*while (RootNode.Parent != null) {
					Utils.Prepend(ref Bones, new FoamBone(RootNode.Name, -1, NumMatrix4x4.Identity));
					RootNode = RootNode.Parent;
				}*/


				for (int i = 0; i < Bones.Length; i++) {
					Node BoneNode = FindNode(NodeHierarchy, Bones[i].Name);
					int BoneParentIndex = FindBoneIndex(Bones, BoneNode.Parent.Name);

					if (BoneNode != RootNode)
						if (BoneParentIndex == -1)
							throw new Exception("Could not find a bone");

					Bones[i].ParentBoneIndex = BoneParentIndex;
				}
			} else
				Bones = null;

			// Animations
			FoamAnimation[] Animations = null;

			foreach (var Anim in Sc.Animations) {
				string[] BoneNames = Anim.NodeAnimationChannels.Select(C => C.NodeName).ToArray();
				int FrameCount = Anim.NodeAnimationChannels[0].PositionKeyCount;
				FoamAnimationFrame[] Frames = new FoamAnimationFrame[FrameCount];

				for (int i = 0; i < FrameCount; i++)
					Frames[i] = ReadFrame(Anim.NodeAnimationChannels, BoneNames, i);

				FoamAnimation Animation = new FoamAnimation(Anim.Name, Frames, BoneNames, (float)Anim.DurationInTicks, (float)Anim.TicksPerSecond);
				Utils.Append(ref Animations, Animation);
			}

			return new FoamModel(Path.GetFileNameWithoutExtension(FileName), FoamFlags.Model, MeshList.ToArray(), Bones, Animations);
		}

		static FoamBoneInfo FindWeightsFor(Bone[] Bones, FoamBone[] FoamBones, int VertexID) {
			List<float> WeightsList = new List<float>();
			List<int> VertexBonesList = new List<int>();

			for (int i = 0; i < Bones.Length; i++) {
				Bone B = Bones[i];

				foreach (var W in B.VertexWeights)
					if (W.VertexID == VertexID) {
						WeightsList.Add(W.Weight);
						VertexBonesList.Add(FindBoneIndex(FoamBones, B.Name));
					}
			}

			while (WeightsList.Count < 4) {
				WeightsList.Add(0);
				VertexBonesList.Add(0);
			}

			if (WeightsList.Count > 4)
				throw new Exception("More than 4 vertex bone weights not supported");

			FoamBoneInfo BInfo = new FoamBoneInfo();
			BInfo.Bone1 = VertexBonesList[0];
			BInfo.Bone2 = VertexBonesList[1];
			BInfo.Bone3 = VertexBonesList[2];
			BInfo.Bone4 = VertexBonesList[3];

			BInfo.Weight1 = WeightsList[0];
			BInfo.Weight2 = WeightsList[1];
			BInfo.Weight3 = WeightsList[2];
			BInfo.Weight4 = WeightsList[3];
			return BInfo;
		}

		static FoamAnimationFrame ReadFrame(List<NodeAnimationChannel> Channels, string[] BoneNames, int Frame) {
			NumMatrix4x4[] BoneTransforms = new NumMatrix4x4[BoneNames.Length];

			for (int i = 0; i < BoneNames.Length; i++) {
				NodeAnimationChannel Ch = FindBoneChannel(Channels, BoneNames[i]);
				BoneTransforms[i] = GetTransformForFrame(Ch, Frame);
			}

			return new FoamAnimationFrame(BoneTransforms);
		}

		static NodeAnimationChannel FindBoneChannel(List<NodeAnimationChannel> Channels, string Name) {
			foreach (var C in Channels)
				if (C.NodeName == Name)
					return C;

			throw new Exception("Could not find bone channel " + Name);
		}

		static NumMatrix4x4 GetTransformForFrame(NodeAnimationChannel Ch, int Frame) {
			VectorKey PosKey = Ch.PositionKeys[Ch.PositionKeys.Count - 1];
			if (Frame < Ch.PositionKeys.Count)
				PosKey = Ch.PositionKeys[Frame];

			QuaternionKey RotKey = Ch.RotationKeys[Ch.RotationKeys.Count - 1];
			if (Frame < Ch.RotationKeys.Count)
				RotKey = Ch.RotationKeys[Frame];

			VectorKey SclKey = Ch.ScalingKeys[Ch.ScalingKeys.Count - 1];
			if (Frame < Ch.ScalingKeys.Count)
				SclKey = Ch.ScalingKeys[Frame];


			NumMatrix4x4 Rot = NumMatrix4x4.CreateFromQuaternion(ConvertQuat(RotKey.Value));
			NumMatrix4x4 Pos = NumMatrix4x4.CreateTranslation(ConvertVec(PosKey.Value));
			NumMatrix4x4 Scl = NumMatrix4x4.CreateScale(ConvertVec(SclKey.Value));

			//return Pos * Rot * Scl;
			return Scl * Rot * Pos;
		}

		static NumMatrix4x4 ConvertMatrix(AssMatrix4x4 Mat) {
			//return NumMatrix4x4.Transpose(*(NumMatrix4x4*)&Mat);
			//return NumMatrix4x4.Transpose(*(NumMatrix4x4*)&Mat);

			Mat.Decompose(out Vector3D Scaling, out AssQuaternion Rotation, out Vector3D Translation);

			NumMatrix4x4 Rot = NumMatrix4x4.CreateFromQuaternion(ConvertQuat(Rotation));
			NumMatrix4x4 Pos = NumMatrix4x4.CreateTranslation(ConvertVec(Translation));
			NumMatrix4x4 Scl = NumMatrix4x4.CreateScale(ConvertVec(Scaling));
			return Scl * Rot * Pos;
			//*/
		}

		static System.Numerics.Quaternion ConvertQuat(Assimp.Quaternion Q) {
			return new System.Numerics.Quaternion(Q.X, Q.Y, Q.Z, Q.W);
		}

		static Vector3 ConvertVec(Vector3D V) {
			return new Vector3(V.X, V.Y, V.Z);
		}

		static int FindBoneIndex(FoamBone[] Bones, string Name) {
			for (int i = 0; i < Bones.Length; i++)
				if (Bones[i].Name == Name)
					return i;

			return -1;
			//throw new Exception("Bone not found " + Name);
		}

		static bool ContainsBoneNamed(FoamBone[] Bones, string Name) {
			foreach (var B in Bones)
				if (B.Name == Name)
					return true;

			return false;
		}

		static Node FindRoot(Node Node, FoamBone[] Bones) {
			if (FindBoneIndex(Bones, Node.Name) == -1)
				return Node;

			return FindRoot(Node.Parent, Bones);
		}

		static Node FindNode(Node[] Nodes, string Name) {
			for (int i = 0; i < Nodes.Length; i++)
				if (Nodes[i].Name == Name)
					return Nodes[i];

			return null;
		}

		static Node[] Flatten(Node RootNode) {
			List<Node> Nodes = new List<Node>();
			Nodes.Add(RootNode);

			foreach (var C in RootNode.Children)
				Nodes.AddRange(Flatten(C));

			return Nodes.ToArray();
		}

		static void AddTextureIfExists(bool Exists, ref FoamMaterial FoamMat, TextureSlot Texture, FoamTextureType TexType) {
			if (Exists) {
				FoamMat.AddTexture(new FoamTexture(Texture.FilePath, TexType));
			}
		}
	}
}
