using Foam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	static class ObjLoader {
		struct ObjVertex3 {
			public Vector3 Position;
			public Vector2 UV;

			public ObjVertex3(Vector3 Position, Vector2 UV) {
				this.Position = Position;
				this.UV = UV;
			}

			public FoamVertex3 ToFoamVertex() {
				return new FoamVertex3(Position, UV);
			}
		}

		class ObjMesh {
			public string MaterialName;
			public string MeshName;
			public List<ObjVertex3> Vertices;

			public ObjMesh(string MaterialName, string MeshName) {
				this.MaterialName = MaterialName;
				this.MeshName = MeshName;
				Vertices = new List<ObjVertex3>();
			}

			public FoamMesh ToFoamMesh(FoamMaterial[] Materials) {
				FoamVertex3[] Verts = Vertices.Select(V => V.ToFoamVertex()).ToArray();
				ushort[] Inds = new ushort[Verts.Length];
				for (int i = 0; i < Inds.Length; i++)
					Inds[i] = (ushort)i;

				int MaterialIndex = 0;

				for (int i = 0; i < Materials.Length; i++)
					if (Materials[i].MaterialName == MaterialName) {
						MaterialIndex = i;
						break;
					}

				return new FoamMesh(Verts, Inds, null, MeshName, MaterialIndex);
			}
		}



		public static FoamModel Load(string ObjFile, string MtlFile) {
			FoamMaterial[] Materials = new FoamMaterial[] { new FoamMaterial("default") };
			ref FoamMaterial CurMat = ref Materials[0];
			string[] MtlLines = File.ReadAllLines(MtlFile);

			for (int i = 0; i < MtlLines.Length; i++) {
				string Line = MtlLines[i].Trim().Replace('\t', ' ');

				while (Line.Contains("  "))
					Line = Line.Replace("  ", " ");

				if (Line.StartsWith("#") || Line.Length == 0)
					continue;

				string[] Tokens = Line.Split(' ');
				switch (Tokens[0].ToLower()) {
					case "newmtl":
						Utils.Append(ref Materials, new FoamMaterial(Tokens[1]));
						CurMat = ref Materials[Materials.Length - 1];
						break;

					default:
						break;
				}
			}

			List<ObjMesh> Meshes = new List<ObjMesh>();
			ObjMesh CurMesh = null;
			string CurMeshName = "mesh";

			//List<Vertex3> ObjVertices = new List<Vertex3>();

			string[] Lines = File.ReadAllLines(ObjFile);
			List<Vector3> Verts = new List<Vector3>();
			List<Vector2> UVs = new List<Vector2>();

			for (int j = 0; j < Lines.Length; j++) {
				string Line = Lines[j].Trim().Replace('\t', ' ');

				while (Line.Contains("  "))
					Line = Line.Replace("  ", " ");

				if (Line.StartsWith("#") || Line.Length == 0)
					continue;

				string[] Tokens = Line.Split(' ');
				switch (Tokens[0].ToLower()) {
					case "o":
						CurMeshName = Tokens[1];
						break;

					case "v": // Vertex
						Verts.Add(new Vector3(Tokens[1].ParseFloat(), Tokens[2].ParseFloat(), Tokens[3].ParseFloat()));
						break;

					case "vt": // Texture coordinate
						UVs.Add(new Vector2(Tokens[1].ParseFloat(), Tokens[2].ParseFloat()));
						break;

					case "vn": // Normal
						break;

					case "f": // Face
						if (CurMesh == null) {
							CurMesh = new ObjMesh("default", CurMeshName);
							Meshes.Add(CurMesh);
						}

						for (int i = 2; i < Tokens.Length - 1; i++) {
							string[] V = Tokens[1].Split('/');
							CurMesh.Vertices.Add(new ObjVertex3(Verts[V[0].ParseInt(1) - 1], V.Length > 1 ? UVs[V[1].ParseInt(1) - 1] : Vector2.Zero));

							V = Tokens[i].Split('/');
							CurMesh.Vertices.Add(new ObjVertex3(Verts[V[0].ParseInt(1) - 1], V.Length > 1 ? UVs[V[1].ParseInt(1) - 1] : Vector2.Zero));

							V = Tokens[i + 1].Split('/');
							CurMesh.Vertices.Add(new ObjVertex3(Verts[V[0].ParseInt(1) - 1], V.Length > 1 ? UVs[V[1].ParseInt(1) - 1] : Vector2.Zero));
						}

						break;

					case "usemtl":
						CurMesh = Meshes.Where(M => M.MaterialName == Tokens[1]).FirstOrDefault();
						if (CurMesh == null) {
							CurMesh = new ObjMesh(Tokens[1], CurMeshName);
							Meshes.Add(CurMesh);
						}
						break;

					default:
						break;
				}
			}

			return new FoamModel(Path.GetFileNameWithoutExtension(ObjFile), FoamFlags.Level, Meshes.Select(M => M.ToFoamMesh(Materials)).ToArray(), null, null, Materials.ToArray());
		}
	}
}
