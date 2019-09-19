using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct MatDef {
		public int MatID;
		public int Start;
		public int Count;

		public MatDef(int MatID, int Start, int Count) {
			this.MatID = MatID;
			this.Start = Start;
			this.Count = Count;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	unsafe struct LightmapperVertex {
		public Vector3 Pos;
		public Vector2 UV;
	}

	static unsafe class Lightmapper {
		const string DllName = "LightMapper";
		const CallingConvention CConv = CallingConvention.Cdecl;

		[DllImport(DllName, CallingConvention = CConv)]
		public static extern int generate_lightmap(int LightW, int LightH, Vector4* pixels, LightmapperVertex[] verts, int vertcount, ushort[] inds, int indcount, int bounces);

		public static void GenerateLightmap(ref Vector4[] Pixels, int LightW, int LightH, Vector3[] Pos, Vector2[] UV, int Bounces) {
			LightmapperVertex[] Verts = new LightmapperVertex[Pos.Length];
			ushort[] Inds = new ushort[Pos.Length];

			for (int i = 0; i < Verts.Length; i++) {
				Verts[i].Pos = Pos[i];
				Verts[i].UV = UV[i];
				Inds[i] = (ushort)i;
			}

			fixed (Vector4* PixelsPtr = Pixels)
				generate_lightmap(LightW, LightH, PixelsPtr, Verts, Verts.Length, Inds, Inds.Length, Bounces);
		}
	}
}
