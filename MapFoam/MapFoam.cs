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
using System.Text;
using System.Threading.Tasks;
using UVAtlasNET;

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

			if (ObjInput == null) {
				ObjInput = "sample/test.obj";
				MtlInput = "sample/test.mtl";
				MapOutput = "sample/test.mapfoam";
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
			MeshAtlasMap[] AtlasMaps = new MeshAtlasMap[0];

			// Generate UV maps per mesh
			for (int i = 0; i < LevelModel.Meshes.Length; i++) {
				Console.WriteLine("Generating atlas for mesh #" + i);

				LevelModel.Meshes[i] = GenAtlas(OutDir, LevelModel.Meshes[i], out MeshAtlasMap AtlasMap);
				Utils.Append(ref AtlasMaps, AtlasMap);
			}

			if (ComputeLights) {
				Light[] Lights = new Light[] { new Light(new Vector3(50, -100, 50)) };

				LightMapping.Compute(LevelModel, AtlasMaps, Lights);

				foreach (var AtlasMap in AtlasMaps) {
					string TexName = AtlasMap.Mesh.MeshName + ".png";

					ref FoamMaterial Mat = ref LevelModel.Materials[AtlasMap.Mesh.MaterialIndex];
					Utils.Append(ref Mat.Textures, new FoamTexture(TexName, FoamTextureType.Diffuse));

					//AtlasMap.Atlas.Resize(0.5f);
					AtlasMap.Atlas.Save(Path.Combine(OutDir, TexName));
				}
			}

			LevelModel.SaveToFile(MapOutput);
		}

		static FoamMesh GenAtlas(string OutDir, FoamMesh Msh, out MeshAtlasMap AtlasMap) {
			IntPtr _Atlas = XAtlas.Create();

			XAtlas_MeshDecl MeshDecl = new XAtlas_MeshDecl(Msh.GetFlatVertices().Select(V => V.Position).ToArray());
			XAtlas.AddMesh(_Atlas, ref MeshDecl);






			FoamVertex3[] Verts = Msh.GetFlatVertices();

			int[] Inds = new int[Verts.Length];
			for (int i = 0; i < Inds.Length; i++)
				Inds[i] = i;

			Vector2[] UVs = GenAtlas(1024, Verts.Select(V => V.Position).ToArray(), Inds, out FastBitmap Atlas);
			for (int i = 0; i < UVs.Length; i++)
				Verts[i].UV = UVs[i];

			AtlasMap = new MeshAtlasMap(Atlas.Width, Atlas.Height, Atlas, Msh);

			Console.WriteLine("Filling atlas with world positions");
			Vector2 AtlasSize = new Vector2(Atlas.Width, Atlas.Height);
			const int AABBOffset = 1;

			for (int i = 0; i < Verts.Length; i += 3) {
				FoamVertex3 A = Verts[i];
				FoamVertex3 B = Verts[i + 1];
				FoamVertex3 C = Verts[i + 2];

				Vector2 AUV = A.UV;
				Vector2 BUV = B.UV;
				Vector2 CUV = C.UV;
				Vector2 UVCenter = new Vector2((AUV.X + BUV.X + CUV.X) / 3, (AUV.Y + BUV.Y + CUV.Y) / 3);

				Vector2 UVOffset = (new Vector2(1) / AtlasSize) * 20;
				//UVOffset = Vector2.Zero;

				AUV += Vector2.Normalize(AUV - UVCenter) * UVOffset;
				BUV += Vector2.Normalize(BUV - UVCenter) * UVOffset;
				CUV += Vector2.Normalize(CUV - UVCenter) * UVOffset;

				Vector2 Min = Utils.Round(Utils.Min(Utils.Min(AUV, BUV), CUV) * AtlasSize) - new Vector2(AABBOffset);
				Vector2 Max = Utils.Round(Utils.Max(Utils.Max(AUV, BUV), CUV) * AtlasSize) + new Vector2(AABBOffset);

				for (int X = (int)Min.X; X < (int)Max.X; X++)
					for (int Y = (int)Min.Y; Y < (int)Max.Y; Y++) {
						if (X < 0 || X >= Atlas.Width - 1 || Y < 0 || Y >= Atlas.Height - 1)
							continue;

						float Dist = 0;
						//Atlas.SetPixel(X, Y, new FastColor(0, 255, 0));

						Vector3 Bary = Barycentric(X, Y, AUV * AtlasSize, BUV * AtlasSize, CUV * AtlasSize);
						if (Bary.X < Dist || Bary.Y < Dist || Bary.Z < Dist)
							continue;

						Bary = Barycentric(X, Y, A.UV * AtlasSize, B.UV * AtlasSize, C.UV * AtlasSize);

						Vector3 Normal = Vector3.Normalize(Vector3.Cross(C.Position - B.Position, A.Position - B.Position));
						AtlasMap.Set(X, Y, (A.Position * Bary.X) + (B.Position * Bary.Y) + (C.Position * Bary.Z), Normal);
					}
			}

			Atlas.Save(Path.Combine(OutDir, Msh.MeshName + ".png"));

			Msh.Vertices = Verts;
			Msh.Indices = Inds.Select(I => (ushort)I).ToArray();
			return Msh;
		}

		static Vector3 Barycentric(float X, float Y, Vector2 A, Vector2 B, Vector2 C) {
			float Area = 0.5f * (-B.Y * C.X + A.Y * (-B.X + C.X) + A.X * (B.Y - C.Y) + B.X * C.Y);
			float s = 1 / (2 * Area) * (A.Y * C.X - A.X * C.Y + (C.Y - A.Y) * X + (A.X - C.X) * Y);
			float t = 1 / (2 * Area) * (A.X * B.Y - A.Y * B.X + (A.Y - B.Y) * X + (B.X - A.X) * Y);

			return new Vector3(1 - (s + t), s, t);
		}

		static Vector2[] GenAtlas(int AtlasSize, Vector3[] Points, int[] Inds, out FastBitmap UVBmp) {
			float[] PointsX = Points.Select(P => P.X).ToArray();
			float[] PointsY = Points.Select(P => P.Y).ToArray();
			float[] PointsZ = Points.Select(P => P.Z).ToArray();

			float[] U;
			float[] V;
			int[] NewInds;
			int[] VertRemap;

			UVAtlas.ReturnCode Ret = UVAtlas.Atlas(PointsX, PointsY, PointsZ, Inds, out U, out V, out NewInds, out VertRemap,
				int.MaxValue, 0, 20, AtlasSize, AtlasSize, UVAtlas.Quality.UVATLAS_GEODESIC_QUALITY, 0);

			if (Ret != UVAtlas.ReturnCode.SUCCESS)
				throw new Exception("UVAtlas failed with " + Ret);

			Vector2[] UVs = new Vector2[U.Length];
			for (int i = 0; i < UVs.Length; i++)
				UVs[i] = Utils.Min(Vector2.One, Utils.Max(Vector2.Zero, new Vector2(U[i], V[i])));

			UVBmp = new FastBitmap(AtlasSize, AtlasSize);
			return UVs;
		}
	}

	class MeshAtlasMap {
		public int Width;
		public int Height;
		public FastBitmap Atlas;
		public FoamMesh Mesh;

		Vector3?[] Pos;
		Vector3[] Normals;

		public MeshAtlasMap(int Width, int Height, FastBitmap Atlas, FoamMesh Mesh) {
			this.Width = Width;
			this.Height = Height;
			this.Atlas = Atlas;
			this.Mesh = Mesh;

			Pos = new Vector3?[Width * Height];
			Normals = new Vector3[Width * Height];
		}

		public bool TryGet(int X, int Y, out Vector3 WorldPos, out Vector3 Normal) {
			int Idx = Y * Width + X;
			Vector3? Val = Pos[Idx];
			Normal = Normals[Idx];
			WorldPos = Val ?? Vector3.Zero;
			return Val.HasValue;
		}

		public void Set(int X, int Y, Vector3? Val, Vector3 Normal) {
			int Idx = Y * Width + X;
			Pos[Idx] = Val;
			Normals[Idx] = Normal;
		}
	}
}
