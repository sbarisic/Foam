using Assimp;
using Assimp.Configs;
using Foam;
using FoamCompile.Loaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FoamCompile {
	static class Program {
		static AssimpContext Importer;

		static void Main(string[] args) {
			/*FoamMesh[] WalkerMesh = Obj.Load("models/obj/walker/walker.obj");

			FoamHeader Foam = new FoamHeader("walker", FoamFlags.Static, WalkerMesh);
			Foam.SaveToFile("walker.foam");*/

			FoamMesh[] Msh = Load("models/md3/watercan/watercan.md3");
			FoamHeader Foam = new FoamHeader("watercan", FoamFlags.Static, Msh);
			Foam.SaveToFile("watercan.foam");
		}

		static FoamMesh[] Load(string FileName) {
			if (Importer == null) {
				Importer = new AssimpContext();
			}

			Scene Sc = Importer.ImportFile(FileName);
			List<FoamMesh> MeshList = new List<FoamMesh>();

			foreach (var M in Sc.Meshes) {
				Vector3D[] Verts = M.Vertices.ToArray();
				Vector3D[] UVs = M.TextureCoordinateChannels[0].ToArray();
				string Name = M.Name;

				FoamVertex3[] FoamVertices = new FoamVertex3[Verts.Length];
				for (int i = 0; i < FoamVertices.Length; i++) {
					FoamVertex3 V = new FoamVertex3(new Vector3(Verts[i].X, Verts[i].Y, Verts[i].Z), new Vector2(UVs[i].X, UVs[i].Y));
					FoamVertices[i] = V;
				}

				List<ushort> FoamIndices = new List<ushort>();
				foreach (var F in M.Faces)
					foreach (var FaceIndex in F.Indices)
						FoamIndices.Add((ushort)FaceIndex);

				MeshList.Add(new FoamMesh(FoamVertices, FoamIndices.ToArray(), Name));
			}

			return MeshList.ToArray();
		}
	}
}
