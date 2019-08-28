using Foam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FoamCompile.Loaders {
	public static class Obj {
		class ObjMesh {
			public List<FoamVertex3> Vertices;
			public string MaterialName;

			public ObjMesh(string MaterialName) {
				this.MaterialName = MaterialName;
				Vertices = new List<FoamVertex3>();
			}

			public FoamMesh ToFoamMesh() {
				return new FoamMesh(Vertices.ToArray(), null, MaterialName);
			}
		}

		public static FoamMesh[] Load(string FileName) {
			List<ObjMesh> Meshes = new List<ObjMesh>();
			ObjMesh CurMesh = null;

			//List<Vertex3> ObjVertices = new List<Vertex3>();

			string[] Lines = File.ReadAllLines(FileName);
			List<Vector3> Verts = new List<Vector3>();
			List<Vector2> UVs = new List<Vector2>();

			for (int j = 0; j < Lines.Length; j++) {
				string Line = Lines[j].Trim().Replace('\t', ' ');

				while (Line.Contains("  "))
					Line = Line.Replace("  ", " ");

				if (Line.StartsWith("#"))
					continue;

				string[] Tokens = Line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				switch (Tokens[0].ToLower()) {
					case "o":
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
							CurMesh = new ObjMesh("default");
							Meshes.Add(CurMesh);
						}

						for (int i = 2; i < Tokens.Length - 1; i++) {
							string[] V = Tokens[1].Split('/');
							CurMesh.Vertices.Add(new FoamVertex3(Verts[V[0].ParseInt(1) - 1], V.Length > 1 ? UVs[V[1].ParseInt(1) - 1] : Vector2.Zero, Vector3.Zero, FoamColor.White));

							V = Tokens[i].Split('/');
							CurMesh.Vertices.Add(new FoamVertex3(Verts[V[0].ParseInt(1) - 1], V.Length > 1 ? UVs[V[1].ParseInt(1) - 1] : Vector2.Zero, Vector3.Zero, FoamColor.White));

							V = Tokens[i + 1].Split('/');
							CurMesh.Vertices.Add(new FoamVertex3(Verts[V[0].ParseInt(1) - 1], V.Length > 1 ? UVs[V[1].ParseInt(1) - 1] : Vector2.Zero, Vector3.Zero, FoamColor.White));
						}

						break;

					case "usemtl":
						CurMesh = Meshes.Where(M => M.MaterialName == Tokens[1]).FirstOrDefault();

						if (CurMesh == null) {
							CurMesh = new ObjMesh(Tokens[1]);
							Meshes.Add(CurMesh);
						}

						break;

					default:
						break;
				}
			}

			return Meshes.Select(M => M.ToFoamMesh()).ToArray();
		}
	}
}
