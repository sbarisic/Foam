using CARP;
using Foam;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UVAtlasNET;

namespace MapFoam {
	class Program {
		static void Main(string[] args) {
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

			if (!File.Exists(ObjInput))
				throw new Exception("Obj input file not found");

			if (!File.Exists(MtlInput))
				throw new Exception("Mtl input file not found");

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

				LevelModel.Meshes[i] = GenAtlas(LevelModel.Meshes[i], out MeshAtlasMap AtlasMap);
				Utils.Append(ref AtlasMaps, AtlasMap);
			}

			if (ComputeLights) {
				LightMapping.Compute(LevelModel, AtlasMaps);

				foreach (var AtlasMap in AtlasMaps) {
					string TexName = AtlasMap.Mesh.MeshName + ".png";

					ref FoamMaterial Mat = ref LevelModel.Materials[AtlasMap.Mesh.MaterialIndex];
					Utils.Append(ref Mat.Textures, new FoamTexture(TexName, FoamTextureType.Diffuse));

					AtlasMap.Atlas.Save(AtlasMap.Mesh.MeshName + ".png");
				}
			}

			LevelModel.SaveToFile(MapOutput);
		}

		static FoamMesh GenAtlas(FoamMesh Msh, out MeshAtlasMap AtlasMap) {
			FoamVertex3[] Verts = Msh.GetFlatVertices();

			int[] Inds = new int[Verts.Length];
			for (int i = 0; i < Inds.Length; i++)
				Inds[i] = i;

			Vector2[] UVs = GenAtlas(Verts.Select(V => V.Position).ToArray(), Inds, out Bitmap Atlas);
			for (int i = 0; i < UVs.Length; i++)
				Verts[i].UV = UVs[i];

			AtlasMap = new MeshAtlasMap(Atlas.Width, Atlas.Height, Atlas, Msh);

			Vector2 ImgSize = new Vector2(Atlas.Width, Atlas.Height);
			for (int Y = 0; Y < Atlas.Height; Y++)
				for (int X = 0; X < Atlas.Width; X++) {
					Atlas.SetPixel(X, Y, Color.Transparent);
					AtlasMap.Set(X, Y, null, Vector3.Zero);
					Vector2 ImgUV = new Vector2(X, Y) / ImgSize;

					for (int i = 0; i < Verts.Length; i += 3) {
						FoamVertex3 A = Verts[i];
						FoamVertex3 B = Verts[i + 1];
						FoamVertex3 C = Verts[i + 2];

						Vector3 Bary = Barycentric(ImgUV, A.UV, B.UV, C.UV);
						if (Bary.X > 0 && Bary.Y > 0 && Bary.Z > 0) {
							Atlas.SetPixel(X, Y, Color.Red);

							Vector3 Normal = Vector3.Normalize(Vector3.Cross(C.Position - B.Position, A.Position - B.Position));
							AtlasMap.Set(X, Y, (A.Position * Bary.X) + (B.Position * Bary.Y) + (C.Position * Bary.Z), Normal);
							break;
						}
					}
				}

			Atlas.Save(Msh.MeshName + ".png");

			Msh.Vertices = Verts;
			Msh.Indices = Inds.Select(I => (ushort)I).ToArray();
			return Msh;
		}

		static Vector3 Barycentric(Vector2 Pt, Vector2 A, Vector2 B, Vector2 C) {
			float Area = 0.5f * (-B.Y * C.X + A.Y * (-B.X + C.X) + A.X * (B.Y - C.Y) + B.X * C.Y);
			float s = 1 / (2 * Area) * (A.Y * C.X - A.X * C.Y + (C.Y - A.Y) * Pt.X + (A.X - C.X) * Pt.Y);
			float t = 1 / (2 * Area) * (A.X * B.Y - A.Y * B.X + (A.Y - B.Y) * Pt.X + (B.X - A.X) * Pt.Y);

			return new Vector3(s, t, 1 - (s + t));
		}

		static Vector2[] GenAtlas(Vector3[] Points, int[] Inds, out Bitmap UVBmp) {
			const int W = 512;
			const int H = 512;

			float[] PointsX = Points.Select(P => P.X).ToArray();
			float[] PointsY = Points.Select(P => P.Y).ToArray();
			float[] PointsZ = Points.Select(P => P.Z).ToArray();

			float[] U;
			float[] V;
			int[] NewInds;
			int[] VertRemap;

			UVAtlas.ReturnCode Ret = UVAtlas.Atlas(PointsX, PointsY, PointsZ, Inds, out U, out V, out NewInds, out VertRemap, width: W, height: H);

			if (Ret != UVAtlas.ReturnCode.SUCCESS)
				throw new Exception("UVAtlas failed with " + Ret);

			Vector2[] UVs = new Vector2[U.Length];
			for (int i = 0; i < UVs.Length; i++)
				UVs[i] = new Vector2(U[i], V[i]);

			UVBmp = new Bitmap(W, H);

			return UVs;
		}
	}

	class MeshAtlasMap {
		public int Width;
		public int Height;
		public Bitmap Atlas;
		public FoamMesh Mesh;

		Vector3?[] Pos;
		Vector3[] Normals;

		public MeshAtlasMap(int Width, int Height, Bitmap Atlas, FoamMesh Mesh) {
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
