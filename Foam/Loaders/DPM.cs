using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// TODO
// Darkplaces model files

// https://www.quakewiki.net/darkplaces-wiki/modeling-for-dp/
// https://github.com/xonotic/darkplaces

namespace Foam.Loaders {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct DPMHeader {
		public uint ofs_frames; // dpmframe_t frame[num_frames];
		public uint ofs_meshs; // dpmmesh_t mesh[num_meshs];
		public uint ofs_bones; // dpmbone_t bone[num_bones];
		public uint num_frames;
		public uint num_meshs;

		public uint num_bones;

		public float AllRadius;
		public float YawRadius;
		public Vector3 Maxs;
		public Vector3 Mins;
		public uint filesize; // size of entire model file
		public uint type; // 2 (hierarchical skeletal pose)

		public fixed byte ID[16];

		public string GetMagic() {
			fixed (byte* BytePtr = ID) {
				string Str = Encoding.ASCII.GetString(Utils.ReadBytes(BytePtr, 16, true).Take(15).ToArray());
				return Str;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct DPMMesh {
		// these offsets are relative to the file
		public uint ofs_groupids; // unsigned int groupids[numtris]; // the meaning of these values is entirely up to the gamecode and modeler
		public uint ofs_indices; // unsigned int indices[numtris*3]; // designed for glDrawElements (each triangle is 3 unsigned int indices)
		public uint ofs_texcoords; // float texcoords[numvertices][2];
		public uint ofs_verts; // dpmvertex_t vert[numvertices]; // see vertex struct
		public uint num_tris;
		public uint num_verts;
		public fixed byte shadername[32]; // name of the shader to use

		public string GetShaderName() {
			fixed (byte* BytePtr = shadername) {
				string Str = Encoding.ASCII.GetString(Utils.ReadBytes(BytePtr, 32, true));
				return Str;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct DPMVertex {
		public uint numbones;
	}


	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct DPMBoneVert {
		uint bonenum; // number of the bone
		Vector3 normal; // surface normal (these blend)

		float influence; // weight
		Vector3 origin; // vertex location (these blend)
	}

	public class DPM : ModelLoader {
		public bool CanLoad(Stream S, string FileName) {
			// not implemented yet
			return false;

			using (BinaryReader Reader = new BinaryReader(S, Encoding.ASCII, true)) {
				DPMHeader Header = Reader.ReadStructReverse<DPMHeader>();

				if (Header.GetMagic() == "DARKPLACESMODEL")
					return true;
			}

			return false;
		}

		public FoamModel Load(Stream S, string FileName) {
			FoamMesh[] Meshes = null;

			using (BinaryReader Reader = new BinaryReader(S, Encoding.ASCII, true)) {
				DPMHeader Header = Reader.ReadStructReverse<DPMHeader>();

				Reader.Seek(Header.ofs_meshs);
				Meshes = Reader.ReadStructArrayReverse<DPMMesh>((int)Header.num_meshs).Select(M => LoadMesh(Reader, M)).ToArray();
			}

			return null;
		}

		FoamMesh LoadMesh(BinaryReader Reader, DPMMesh Msh) {
			FoamVertex3[] Verts = new FoamVertex3[Msh.num_verts];
			FoamBoneInfo[] Info = new FoamBoneInfo[Verts.Length];
			ushort[] Inds = new ushort[Msh.num_tris * 3];

			Reader.Seek(Msh.ofs_verts);
			for (int i = 0; i < Verts.Length; i++) {
				DPMVertex V = Reader.ReadStructReverse<DPMVertex>();


				for (int j = 0; j < V.numbones; j++) {
					DPMBoneVert BoneVert = Reader.ReadStructReverse<DPMBoneVert>();
				}
			}

			return new FoamMesh(Verts, Inds, Info, Msh.GetShaderName(), 0);
		}
	}
}
