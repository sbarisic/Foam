﻿using Foam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
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

		public MeshAtlasMap(int Width, int Height, FoamMesh Mesh) : this(Width, Height, new FastBitmap(Width, Height), Mesh) {
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
