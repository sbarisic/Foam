using Foam;
using libTech.FileSystem;
using SourceUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using nVector2 = System.Numerics.Vector2;
using nVector3 = System.Numerics.Vector3;
using uVector3 = SourceUtils.Vector3;

// https://github.com/sbarisic/libTech/blob/master/libTech/Models/SourceMdl.cs
// https://github.com/sbarisic/libTech/blob/master/libTech/Models/libTechModel.cs

namespace MdlToFoam {
	unsafe static class MdlToFoam {
		static VirtualFileSystem VFS;
		static bool ConvertToCCW = true;

		static void Main(string[] args) {
			string GameDir = "C:/Program Files (x86)/Steam/steamapps/common/GarrysMod";
			VFS = new VirtualFileSystem(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
			VFS.GetSourceProvider().AddRoot(GameDir);

			Convert("models/props_c17/oildrum001_explosive.mdl");
		}

		static void Convert(string FilePath) {
			Console.WriteLine("Converting {0}", FilePath);

			string FileName = Path.GetFileNameWithoutExtension(FilePath);
			FoamModel Model = LoadMdl(FilePath);

			if (!Directory.Exists("foam"))
				Directory.CreateDirectory("foam");

			Model.SaveToFile("foam/" + FileName + ".foam");
		}

		static FoamModel LoadMdl(string FilePath) {
			FilePath = FilePath.Substring(0, FilePath.Length - Path.GetExtension(FilePath).Length);

			StudioModelFile Mdl = StudioModelFile.FromProvider(FilePath + ".mdl", VFS);
			if (Mdl == null)
				throw new FileNotFoundException("File not found", FilePath + ".mdl");

			ValveVertexFile Verts = ValveVertexFile.FromProvider(FilePath + ".vvd", VFS);
			ValveTriangleFile Tris = ValveTriangleFile.FromProvider(FilePath + ".dx90.vtx", Mdl, Verts, VFS);


			// Foam stuff

			List<FoamMaterial> FoamMaterials = new List<FoamMaterial>();
			string[] TexNames = Mdl.TextureNames.ToArray();

			for (int i = 0; i < Mdl.MaterialCount; i++) {
				string MatName = Mdl.GetMaterialName(i, VFS);
				string ShortMatName = Path.GetFileNameWithoutExtension(MatName);

				ValveMaterialFile VMF = ValveMaterialFile.FromProvider(MatName, VFS);
				FoamMaterials.Add(new FoamMaterial(ShortMatName, new FoamTexture[] { new FoamTexture(TexNames[i], FoamTextureType.Diffuse) }));
			}

			List<FoamMesh> FoamMeshes = new List<FoamMesh>();
			List<FoamBone> FoamBones = new List<FoamBone>();

			StudioModelFile.StudioBone[] Bones = Mdl.GetBones();
			for (int i = 0; i < Bones.Length; i++) {
				string BoneName = Mdl.GetBoneName(i);
				FoamBones.Add(new FoamBone(BoneName, Bones[i].Parent, Matrix4x4.Identity));
			}

			// BODIES
			for (int BodyPartIdx = 0; BodyPartIdx < Mdl.BodyPartCount; BodyPartIdx++) {
				StudioModelFile.StudioModel[] StudioModels = Mdl.GetModels(BodyPartIdx).ToArray();

				// MODELS
				for (int ModelIdx = 0; ModelIdx < StudioModels.Length; ModelIdx++) {
					ref StudioModelFile.StudioModel StudioModel = ref StudioModels[ModelIdx];
					StudioModelFile.StudioMesh[] StudioMeshes = Mdl.GetMeshes(ref StudioModel).ToArray();

					// MESHES
					for (int MeshIdx = 0; MeshIdx < StudioMeshes.Length; MeshIdx++) {
						ref StudioModelFile.StudioMesh StudioMesh = ref StudioMeshes[MeshIdx];

						StudioVertex[] StudioVerts = new StudioVertex[Tris.GetVertexCount(BodyPartIdx, ModelIdx, 0, MeshIdx)];
						Tris.GetVertices(BodyPartIdx, ModelIdx, 0, MeshIdx, StudioVerts);

						int[] Indices = new int[Tris.GetIndexCount(BodyPartIdx, ModelIdx, 0, MeshIdx)];
						Tris.GetIndices(BodyPartIdx, ModelIdx, 0, MeshIdx, Indices);


						// Foam converted
						List<FoamVertex3> FoamVerts = new List<FoamVertex3>(StudioVerts.Select(V => {
							// TODO: CCW
							nVector2 UV = new nVector2(V.TexCoordX, V.TexCoordY);
							return new FoamVertex3(Conv(V.Position), UV, nVector2.Zero, Conv(V.Normal), nVector3.Zero, FoamColor.White);
						}));

						List<FoamBoneInfo> FoamInfo = new List<FoamBoneInfo>(StudioVerts.Select(V => {
							FoamBoneInfo Info = new FoamBoneInfo();
							Info.Bone1 = V.BoneWeights.Bone0;
							Info.Bone2 = V.BoneWeights.Bone1;
							Info.Bone3 = V.BoneWeights.Bone1;

							Info.Weight1 = V.BoneWeights.Weight0;
							Info.Weight2 = V.BoneWeights.Weight1;
							Info.Weight3 = V.BoneWeights.Weight2;
							return Info;
						}));

						List<ushort> FoamInds = new List<ushort>(Indices.Select(I => (ushort)I));

						if (ConvertToCCW)
							FoamInds.Reverse();

						FoamMeshes.Add(new FoamMesh(FoamVerts.ToArray(), FoamInds.ToArray(), FoamInfo.ToArray(), StudioModel.Name + ";" + MeshIdx, StudioMesh.Material));

						/*List<FoamVertex3> Vts = new List<FoamVertex3>();
						for (int i = 0; i < Indices.Length; i++) {
							ref StudioVertex V = ref StudioVerts[Indices[i]];
							Vts.Add(new Vertex3(new Vector3(V.Position.X, V.Position.Y, V.Position.Z), new Vector2(V.TexCoordX, 1.0f - V.TexCoordY), Color.White));
						}*/

						/*string MatName = MaterialNames[StudioMesh.Material];
						Material Mat = Engine.GetMaterial(MatName);

						if (Mat == Engine.GetMaterial("error")) {
							Mat = ValveMaterial.CreateMaterial(MatName);

							if (Mat != Engine.GetMaterial("error"))
								Engine.RegisterMaterial(Mat);
						}

						libTechMesh Msh = new libTechMesh(Vts.ToArray(), Mat);
						Msh.Name = StudioModel.Name;
						Model.AddMesh(Msh);*/
					}
				}
			}


			//*/

			return new FoamModel(Path.GetFileNameWithoutExtension(FilePath), FoamFlags.Model, FoamMeshes.ToArray(), FoamBones.ToArray(), null, FoamMaterials.ToArray());
		}

		static nVector3 Conv(uVector3 V) {
			return new nVector3(V.X, V.Y, V.Z);
		}
	}
}
