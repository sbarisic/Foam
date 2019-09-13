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
				Light[] Lights = new Light[] {
					new Light(new Vector3(-32, 104, 368), new Vector3(1, 0, 0), 10000),
					new Light(new Vector3(-80, 104, 704), new Vector3(0, 1, 0), 10000),
					new Light(new Vector3(0, 312, 304), new Vector3(0, 0, 1), 20000)
				};

				LightMapping.Compute(LevelModel, AtlasMaps, Lights);

				foreach (var AtlasMap in AtlasMaps) {
					string TexName = AtlasMap.Mesh.MeshName + ".png";

					ref FoamMaterial Mat = ref LevelModel.Materials[AtlasMap.Mesh.MaterialIndex];
					Utils.Append(ref Mat.Textures, new FoamTexture(TexName, FoamTextureType.Diffuse));

					//AtlasMap.Atlas.Resize(4);
					AtlasMap.Atlas.Save(Path.Combine(OutDir, TexName));
				}
			}

			LevelModel.SaveToFile(MapOutput);
		}

		static FoamMesh GenAtlas(string OutDir, FoamMesh Msh, out MeshAtlasMap AtlasMap) {
			AtlasStruct* _Atlas = XAtlas.Create();

			XAtlas_MeshDecl MeshDecl = new XAtlas_MeshDecl(Msh.GetFlatVertices().Select(V => V.Position).ToArray());
			XAtlas.AddMesh(_Atlas, ref MeshDecl);

			XAtlas.Generate(_Atlas, ChartOptions.CreateOptions(), null, PackOptions.CreatePackOptions());

			FoamVertex3[] Verts = null;
			ushort[] NewInds = null;

			XAtlasMesh* XMsh = &_Atlas->meshes[0];
			for (int i = 0; i < XMsh->indexCount; i++)
				Utils.Append(ref NewInds, (ushort)XMsh->indexArray[i]);

			Vector2 AtlasSize = new Vector2((int)_Atlas->width, (int)_Atlas->height);

			for (int i = 0; i < XMsh->vertexCount; i++) {
				XAtlasVertex* XVert = &XMsh->vertexArray[i];
				FoamVertex3 OldVert = Msh.Vertices[XVert->xref];

				OldVert.UV = (XVert->UV);

				//OldVert.UV.X = (int)OldVert.UV.X;
				//OldVert.UV.Y = (int)OldVert.UV.Y;

				Utils.Append(ref Verts, OldVert);
			}

			AtlasMap = new MeshAtlasMap((int)_Atlas->width, (int)_Atlas->height, Msh);
			Random Rnd = new Random();

			for (int i = 0; i < Verts.Length; i += 3) {
				ref FoamVertex3 A = ref Verts[i];
				ref FoamVertex3 B = ref Verts[i + 1];
				ref FoamVertex3 C = ref Verts[i + 2];

				int TriangleIndex = i / 3;
				FastColor TriangleColor = FastColor.FromInt(TriangleIndex);

				Vector2 AUV = (A.UV);
				Vector2 BUV = (B.UV);
				Vector2 CUV = (C.UV);
				//Vector2 UVCenter = (AUV + BUV + CUV) / 3;

				AtlasMap.Atlas.UseGraphics((G) => {
					G.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
					G.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					G.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
					Pen DrawPen = new Pen(Color.FromArgb(TriangleColor.A, TriangleColor.R, TriangleColor.G, TriangleColor.B));

					G.DrawLine(DrawPen, AUV.X, AUV.Y, BUV.X, BUV.Y);
					G.DrawLine(DrawPen, AUV.X, AUV.Y, CUV.X, CUV.Y);
					G.DrawLine(DrawPen, BUV.X, BUV.Y, CUV.X, CUV.Y);
				});

				DrawTriangle(AtlasMap.Atlas, TriangleColor, AUV, BUV, CUV);
			}

			AtlasMap.Atlas = AtlasMap.Atlas.Extend();
			AtlasMap.Atlas.Save(Path.Combine(OutDir, Msh.MeshName + ".png"));

			for (int Y = 0; Y < AtlasMap.Atlas.Height; Y++)
				for (int X = 0; X < AtlasMap.Atlas.Width; X++) {
					FastColor Clr = AtlasMap.Atlas.GetPixel(X, Y);
					if (FastColor.IsAlpha(Clr))
						continue;

					int TriangleIndex = Clr.ToInt() * 3;
					ref FoamVertex3 A = ref Verts[TriangleIndex];
					ref FoamVertex3 B = ref Verts[TriangleIndex + 1];
					ref FoamVertex3 C = ref Verts[TriangleIndex + 2];

					Vector3 Bary = Barycentric(X, Y, A.UV, B.UV, C.UV);
					Vector3 Normal = Vector3.Normalize(Vector3.Cross(C.Position - B.Position, A.Position - B.Position));
					Vector3 WorldPos = (A.Position * Bary.X) + (B.Position * Bary.Y) + (C.Position * Bary.Z);

					AtlasMap.Set(X, Y, WorldPos, Normal);
				}

			for (int i = 0; i < Verts.Length; i++) {
				ref FoamVertex3 V = ref Verts[i];
				V.UV /= AtlasSize;
			}

			Msh.Vertices = Verts.ToArray();
			Msh.Indices = NewInds.ToArray();
			return Msh;
		}

		static void DrawTriangle(FastBitmap Image, FastColor Color, Vector2 T0, Vector2 T1, Vector2 T2) {
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
					Image.SetPixel(j, (int)(T0.Y + i), Color); // attention, due to int casts t0.y+i != A.y 
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

		/*static Vector3 Barycentric(double X, double Y, Vector2 A, Vector2 B, Vector2 C) {
			double Area = 0.5 * (-B.Y * C.X + A.Y * (-B.X + C.X) + A.X * (B.Y - C.Y) + B.X * C.Y);
			double s = 1.0 / (2.0 * Area) * (A.Y * C.X - A.X * C.Y + (C.Y - A.Y) * X + (A.X - C.X) * Y);
			double t = 1.0 / (2.0 * Area) * (A.X * B.Y - A.Y * B.X + (A.Y - B.Y) * X + (B.X - A.X) * Y);

			return new Vector3((float)(1 - (s + t)), (float)s, (float)t);
		}*/

		/*static Vector2[] GenAtlas(int AtlasSize, Vector3[] Points, int[] Inds, out FastBitmap UVBmp) {
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
		}*/
	}
}
