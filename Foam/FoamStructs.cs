using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Foam {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe class FoamMesh {
		public FoamVertex3[] Vertices;
		public ushort[] Indices;
		public string MaterialName;

		public FoamMesh(FoamVertex3[] Vertices, ushort[] Indices, string MaterialName) {
			this.Vertices = Vertices;
			this.Indices = Indices;
			this.MaterialName = MaterialName;
		}

		public FoamVertex3[] GetFlatVertices() {
			if (Indices == null)
				return Vertices;

			List<FoamVertex3> Verts = new List<FoamVertex3>();

			for (int i = 0; i < Indices.Length; i++)
				Verts.Add(Vertices[Indices[i]]);

			return Verts.ToArray();
		}

		public override string ToString() {
			return string.Format("[{0}] {1}{2}", Vertices.Length, MaterialName, Indices != null ? " (Indices)" : "");
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteStructArray(Vertices);

			if (Indices != null)
				Writer.WriteStructArray(Indices);
			else
				Writer.Write(0);

			Writer.WriteUTF8String(MaterialName);
		}

		public void Read(BinaryReader Reader) {
			Vertices = Reader.ReadStructArray<FoamVertex3>();

			Indices = Reader.ReadStructArray<ushort>();
			if (Indices.Length == 0)
				Indices = null;

			MaterialName = Reader.ReadUTF8String();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamColor {
		public readonly static FoamColor White = new FoamColor(255, 255, 255);

		public byte R;
		public byte G;
		public byte B;
		public byte A;

		public FoamColor(byte R, byte G, byte B, byte A) {
			this.R = R;
			this.G = G;
			this.B = B;
			this.A = A;
		}

		public FoamColor(byte R, byte G, byte B) : this(R, G, B, 255) {
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamVertex3 {
		public Vector3 Position;
		public Vector2 UV;
		public Vector3 Normal;
		public FoamColor Color;

		public FoamVertex3(Vector3 Position, Vector2 UV, Vector3 Normal, FoamColor Color) {
			this.Position = Position;
			this.UV = UV;
			this.Normal = Normal;
			this.Color = Color;
		}

		public FoamVertex3(Vector3 Position, Vector2 UV) : this(Position, UV, Vector3.Zero, FoamColor.White) {
		}
	}

	[Flags]
	public enum FoamFlags : int {
		Static,
		Animated,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamHeader {
		public int Magic;
		public int Version;
		public string Name;
		public FoamFlags Flags;
		public FoamMesh[] Meshes;

		public FoamHeader(string Name, FoamFlags Flags, FoamMesh[] Meshes) {
			Magic = 0x6D616F46;
			Version = 1;
			this.Name = Name;
			this.Flags = Flags;
			this.Meshes = Meshes;
		}

		public void Write(BinaryWriter Writer) {
			Writer.Write(Magic);
			Writer.Write(Version);
			Writer.WriteUTF8String(Name);
			Writer.Write((int)Flags);

			Writer.Write(Meshes.Length);
			for (int i = 0; i < Meshes.Length; i++)
				Meshes[i].Write(Writer);
		}

		public void Read(BinaryReader Reader) {
			Magic = Reader.ReadInt32();
			Version = Reader.ReadInt32();
			Name = Reader.ReadUTF8String();
			Flags = (FoamFlags)Reader.ReadInt32();

			int MeshCount = Reader.ReadInt32();
			Meshes = new FoamMesh[MeshCount];

			for (int i = 0; i < MeshCount; i++) {
				Meshes[i] = new FoamMesh(null, null, null);
				Meshes[i].Read(Reader);
			}
		}

		public void SaveToFile(string FileName) {
			using (FileStream FS = File.Open(FileName, FileMode.Create))
			using (BinaryWriter Writer = new BinaryWriter(FS))
				Write(Writer);
		}

		public static FoamHeader FromFile(string FileName) {
			FoamHeader Header = new FoamHeader();

			using (FileStream FS = File.OpenRead(FileName))
			using (BinaryReader Reader = new BinaryReader(FS))
				Header.Read(Reader);

			return Header;
		}
	}
}
