using CARP;
using Foam;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	unsafe class Program {
		static void Main(string[] args) {
			if (Debugger.IsAttached)
				Run();
			else
				try {
					Run();
				} catch (Exception E) {
					Console.WriteLine(E);
					Console.ReadLine();
				}
		}

		static void Run() {
			string ObjInput = ArgumentParser.GetSingle("obj");
			string MtlInput = ArgumentParser.GetSingle("mtl");
			string MapOutput = ArgumentParser.GetSingle("out");
			bool EmbedTextures = ArgumentParser.Defined("e");
			bool ComputeLights = !ArgumentParser.Defined("l");

			string ObjName = "test";

			if (ObjInput == null) {
				ObjInput = "sample/" + ObjName + ".obj";
				MtlInput = "sample/" + ObjName + ".mtl";
				MapOutput = "sample/" + ObjName + ".mapfoam";
			}

			if (!File.Exists(ObjInput))
				throw new Exception("Obj input file not found");

			if (!File.Exists(MtlInput))
				throw new Exception("Mtl input file not found");

			string OutDir = Path.GetDirectoryName(Path.GetFullPath(MapOutput));

			Console.WriteLine("obj = '{0}'", ObjInput);
			Console.WriteLine("mtl = '{0}'", MtlInput);
			Console.WriteLine("out = '{0}'", MapOutput);
			Console.WriteLine("Embed textures? {0}", EmbedTextures);
			Console.WriteLine("Compute lights? {0}", ComputeLights);

			FoamModel LevelModel = ObjLoader.Load(ObjInput, MtlInput);


			MeshAtlasMap AtlasMap;

			LevelModel.CalcBounds(out Vector3 Min, out Vector3 Max);
			Console.WriteLine("Level min = {0}; max = {1}", Min, Max);

			// Generate atlas
			{
				Console.WriteLine("Generating lightmap");
				GenAtlas(OutDir, LevelModel, out AtlasMap);
			}

			if (ComputeLights) {
				Light[] Lights = new Light[] {
					new Light(new Vector3(-32, 104, 368), new Vector3(1, 0, 0), 10000),
					new Light(new Vector3(-80, 104, 704), new Vector3(0, 1, 0), 10000),
					new Light(new Vector3(0, 312, 304), new Vector3(0, 0, 1), 20000)
				};

				//LightMapping.Compute(LevelModel, AtlasMap, Lights);

				//Vector4[] Pixels = new Vector4[1024 * 1024];
				//RayLightmapper.raylight_render_scene(1024, 1024, 512, new Vector3(1, 1, 1), 0, null, 0, null, null, null, ref Pixels);

				{
					string TexName = "lightmap.png";

					FoamMaterial LightmapMat = new FoamMaterial(Path.GetFileNameWithoutExtension(TexName), new[] { new FoamTexture(TexName, FoamTextureType.LightMap) });
					Utils.Append(ref LevelModel.Materials, LightmapMat);

					foreach (var M in LevelModel.Meshes)
						M.MaterialIndex = LevelModel.Materials.Length - 1;

					//AtlasMap.Atlas.Resize(4);
					AtlasMap.Atlas.FlipY();
					AtlasMap.Atlas.Save(Path.Combine(OutDir, TexName));

					Utils.Append(ref LevelModel.Extensions, FoamExtension.CreateEmbeddedPng(TexName, AtlasMap.Atlas.GetImage()));
				}
			}

			//ObjLoader.Save(LevelModel, "sample/EXPORTED.obj");
			LevelModel.SaveToFile(MapOutput);

			//Console.WriteLine("Done!");
			//Console.ReadLine();
		}

		static void GenAtlas(string OutDir, FoamModel LevelModel, out MeshAtlasMap AtlasMap) {
			const bool GenerateNewUVs = true;
			const float TextureScale = 2;
			//const float VertexScale = 1.0f / 100;
			const float VertexScale = 1.0f;
			const int BounceCount = 2;

			FoamMesh[] Meshes = LevelModel.Meshes;

			List<FoamMesh> VertexMeshMap = new List<FoamMesh>();
			List<FoamVertex3> ModelVerts = new List<FoamVertex3>();
			//ushort[] NewInds = null;

			int W;
			int H;

			{
				foreach (var Mesh in Meshes) {
					IEnumerable<FoamVertex3> MeshVerts = Mesh.GetFlatVertices();

					foreach (var V in MeshVerts) {
						VertexMeshMap.Add(Mesh);
						ModelVerts.Add(V);
					}

					Mesh.Indices = new ushort[] { };
					Mesh.Vertices = new FoamVertex3[] { };
				}

				if (GenerateNewUVs) {
					AtlasStruct* _Atlas = XAtlas.Create();
					XAtlas_MeshDecl MeshDecl = new XAtlas_MeshDecl(ModelVerts.Select(V => V.Position).ToArray());
					XAtlas.AddMesh(_Atlas, ref MeshDecl);
					XAtlas.Generate(_Atlas, ChartOptions.CreateOptions(), null, PackOptions.CreatePackOptions());

					XAtlasMesh* XMsh = &_Atlas->meshes[0];

					/*NewInds = null;
					for (int i = 0; i < XMsh->indexCount; i++)
						Utils.Append(ref NewInds, (ushort)XMsh->indexArray[i]);*/

					W = (int)(_Atlas->width * TextureScale);
					H = (int)(_Atlas->height * TextureScale);
					AtlasMap = new MeshAtlasMap(W, H);

					FoamVertex3[] OldModelVerts = ModelVerts.ToArray();
					ModelVerts.Clear();

					FoamMesh[] OldVertexMeshMap = VertexMeshMap.ToArray();
					VertexMeshMap.Clear();

					Vector2 AtlasSize = new Vector2((int)_Atlas->width, (int)_Atlas->height);
					for (int i = 0; i < XMsh->vertexCount; i++) {
						XAtlasVertex* XVert = &XMsh->vertexArray[i];
						FoamVertex3 OldVert = OldModelVerts[(int)XVert->xref];
						FoamMesh OldMesh = OldVertexMeshMap[(int)XVert->xref];

						//OldVert.UV = (XVert->UV) / AtlasSize;
						OldVert.UV2 = (XVert->UV) / AtlasSize;

						//OldVert.UV.X = (int)OldVert.UV.X;
						//OldVert.UV.Y = (int)OldVert.UV.Y;

						ModelVerts.Add(OldVert);
						VertexMeshMap.Add(OldMesh);
					}
				} else {
					/*for (int i = 0; i < ModelVerts.Count; i++)
						Utils.Append(ref NewInds, (ushort)i);*/
					AtlasMap = new MeshAtlasMap(512, 512);
				}
			}


			Vector3[] RL_Pos = new Vector3[ModelVerts.Count];
			Vector2[] RL_UV = new Vector2[ModelVerts.Count];

			// Calculate normals
			for (int i = 0; i < ModelVerts.Count; i += 3) {
				FoamVertex3 VA = ModelVerts[i + 0];
				FoamVertex3 VB = ModelVerts[i + 1];
				FoamVertex3 VC = ModelVerts[i + 2];

				Vector3 CB = VC.Position - VB.Position;
				Vector3 AB = VA.Position - VB.Position;
				Vector3 Normal = Vector3.Normalize(Vector3.Cross(CB, AB));

				VA.Normal = Normal;
				VB.Normal = Normal;
				VC.Normal = Normal;

				ModelVerts[i + 0] = VA;
				ModelVerts[i + 1] = VB;
				ModelVerts[i + 2] = VC;


				RL_Pos[i + 0] = VA.Position * VertexScale;
				RL_Pos[i + 1] = VB.Position * VertexScale;
				RL_Pos[i + 2] = VC.Position * VertexScale;

				RL_UV[i + 0] = VA.UV2;
				RL_UV[i + 1] = VB.UV2;
				RL_UV[i + 2] = VC.UV2;
			}

			Vector2 PixelsSize = new Vector2(W, H);
			Vector4[] Pixels = new Vector4[W * H];
			ApplyEmissive(Pixels, W, H, ModelVerts, VertexMeshMap, LevelModel);

			//int TriIdx = 6;
			//Vector4 TriClr = new Vector4(50.0f / 255, 100.0f / 255, 200.0f / 255, 1);
			//DrawTriangle(Pixels, W, H, TriClr, ModelVerts[TriIdx * 3 + 0].UV2 * PixelsSize, ModelVerts[TriIdx * 3 + 1].UV2 * PixelsSize, ModelVerts[TriIdx * 3 + 2].UV2 * PixelsSize);

			Lightmapper.GenerateLightmap(ref Pixels, W, H, RL_Pos, RL_UV, BounceCount);
			ApplyEmissive(Pixels, W, H, ModelVerts, VertexMeshMap, LevelModel);

			//DrawTriangle(Pixels, W, H, TriClr, ModelVerts[TriIdx * 3 + 0].UV2 * PixelsSize, ModelVerts[TriIdx * 3 + 1].UV2 * PixelsSize, ModelVerts[TriIdx * 3 + 2].UV2 * PixelsSize);


			for (int i = 0; i < Pixels.Length; i++) {
				int X = i % W;
				int Y = (i - X) / W;

				Vector4 Clr = Pixels[i];
				Clr = Utils.Clamp(Clr, Vector4.Zero, Vector4.One);

				AtlasMap.Atlas.SetPixel(X, Y, new FastColor((byte)(Clr.X * 255), (byte)(Clr.Y * 255), (byte)(Clr.Z * 255), 255));
			}


			for (int i = 0; i < VertexMeshMap.Count; i++) {
				FoamMesh CurMesh = VertexMeshMap[i];

				//Utils.Append(ref CurMesh.Indices, (ushort)(NewInds[i]));
				Utils.Append(ref CurMesh.Vertices, ModelVerts[i]);
			}

			for (int i = 0; i < Meshes.Length; i++) {
				int VertCount = Meshes[i].Vertices.Length;
				Meshes[i].Indices = new ushort[VertCount];

				for (int j = 0; j < VertCount; j++)
					Meshes[i].Indices[j] = (ushort)j;
			}

			//Msh.Vertices = Verts.ToArray();
			//Msh.Indices = NewInds.ToArray();
		}

		static void ApplyEmissive(Vector4[] Pixels, int W, int H, List<FoamVertex3> ModelVerts, List<FoamMesh> Meshes, FoamModel Model) {
			Vector4 ClrR = new Vector4(1, 0, 0, 1);
			Vector4 ClrG = new Vector4(0, 1, 0, 1);
			Vector4 ClrB = new Vector4(0, 0, 1, 1);
			Vector4 ClrW = new Vector4(1, 1, 1, 1);

			Vector2 Size = new Vector2(W, H);

			for (int i = 0; i < ModelVerts.Count; i += 3) {
				FoamMaterial Mat = Model.Materials[Meshes[i].MaterialIndex];

				// TODO: Move somewhere else
				switch (Mat.MaterialName) {
					case "base/emissive_red":
						DrawTriangle(Pixels, W, H, ClrR, ModelVerts[i + 0].UV2 * Size, ModelVerts[i + 1].UV2 * Size, ModelVerts[i + 2].UV2 * Size);
						break;

					case "base/emissive_green":
						DrawTriangle(Pixels, W, H, ClrG, ModelVerts[i + 0].UV2 * Size, ModelVerts[i + 1].UV2 * Size, ModelVerts[i + 2].UV2 * Size);
						break;

					case "base/emissive_blue":
						DrawTriangle(Pixels, W, H, ClrB, ModelVerts[i + 0].UV2 * Size, ModelVerts[i + 1].UV2 * Size, ModelVerts[i + 2].UV2 * Size);
						break;

					//case "base/emissive_sky":
					case "base/emissive_white":
						DrawTriangle(Pixels, W, H, ClrW, ModelVerts[i + 0].UV2 * Size, ModelVerts[i + 1].UV2 * Size, ModelVerts[i + 2].UV2 * Size);
						break;

					default:
						break;
				}
			}
		}

		/*
		static void GenAtlasWorldPositions(FoamVertex3[] Verts, MeshAtlasMap AtlasMap) {
			for (int i = 0; i < Verts.Length; i += 3) {
				ref FoamVertex3 A = ref Verts[i];
				ref FoamVertex3 B = ref Verts[i + 1];
				ref FoamVertex3 C = ref Verts[i + 2];

				int TriangleIndex = i / 3;
				FastColor TriangleColor = FastColor.FromInt(TriangleIndex);

				Vector2 AUV = (A.UV2);
				Vector2 BUV = (B.UV2);
				Vector2 CUV = (C.UV2);
				//Vector2 UVCenter = (AUV + BUV + CUV) / 3;

				DrawTriangle(AtlasMap.Atlas, TriangleColor, AUV, BUV, CUV);
			}

			for (int Y = 0; Y < AtlasMap.Atlas.Height; Y++)
				for (int X = 0; X < AtlasMap.Atlas.Width; X++) {
					FastColor Clr = AtlasMap.Atlas.GetPixel(X, Y);
					if (FastColor.IsAlpha(Clr))
						continue;

					int TriangleIndex = Clr.ToInt() * 3;
					ref FoamVertex3 A = ref Verts[TriangleIndex];
					ref FoamVertex3 B = ref Verts[TriangleIndex + 1];
					ref FoamVertex3 C = ref Verts[TriangleIndex + 2];

					Vector3 Bary = Barycentric(X, Y, A.UV2, B.UV2, C.UV2);
					Vector3 Normal = Vector3.Normalize(Vector3.Cross(C.Position - B.Position, A.Position - B.Position));
					Vector3 WorldPos = (A.Position * Bary.X) + (B.Position * Bary.Y) + (C.Position * Bary.Z);

					AtlasMap.Set(X, Y, WorldPos, Normal);
				}
		}
		//*/

		static void DrawTriangle(Vector4[] Pixels, int W, int H, Vector4 Color, Vector2 T0, Vector2 T1, Vector2 T2) {
			if (T0.Y == T1.Y && T0.Y == T2.Y)
				return; // I dont care about degenerate triangles 

			// sort the vertices, t0, t1, t2 lower−to−upper (bubblesort yay!) 
			if (T0.Y > T1.Y)
				Swap(ref T0, ref T1);

			if (T0.Y > T2.Y)
				Swap(ref T0, ref T2);

			if (T1.Y > T2.Y)
				Swap(ref T1, ref T2);

			float TotalHeight = (T2.Y - T0.Y);

			for (int i = 0; i < TotalHeight; i++) {
				bool SecondHalf = i > T1.Y - T0.Y || T1.Y == T0.Y;
				int SegmentHeight = (int)(SecondHalf ? T2.Y - T1.Y : T1.Y - T0.Y);

				float Alpha = i / TotalHeight;
				float Beta = (i - (SecondHalf ? T1.Y - T0.Y : 0)) / SegmentHeight; // be careful: with above conditions no division by zero here 

				Vector2 A = T0 + (T2 - T0) * Alpha;
				Vector2 B = SecondHalf ? T1 + (T2 - T1) * Beta : T0 + (T1 - T0) * Beta;

				if (A.X > B.X)
					Swap(ref A, ref B);

				for (int j = (int)A.X; j <= B.X; j++) {
					int XX = j;
					int YY = (int)(T0.Y + i);

					Pixels[YY * W + XX] = Color;

					//Image.SetPixel(, , Color); // attention, due to int casts t0.y+i != A.y 
				}
			}
		}

		static void Swap<T>(ref T A, ref T B) {
			T Temp = B;
			B = A;
			A = Temp;
		}

		public static Vector3 Barycentric(int PX, int PY, Vector2 A, Vector2 B, Vector2 C) {
			Vector3 U = Vector3.Cross(new Vector3(C.X - A.X, B.X - A.X, A.X - PX), new Vector3(C.Y - A.Y, B.Y - A.Y, A.Y - PY));

			if (Math.Abs(U.Z) < 1)
				return new Vector3(int.MinValue);

			return new Vector3(1.0f - (U.X + U.Y) / U.Z, U.Y / U.Z, U.X / U.Z);
		}
	}
}
