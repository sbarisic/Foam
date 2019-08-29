using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Foam {
	interface IFoam {
		void Write(BinaryWriter Writer);
		void Read(BinaryReader Reader);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamBone : IFoam {
		public string Name;
		public Matrix4x4 BindMatrix;

		public FoamBone(string Name, Matrix4x4 BindMatrix) {
			this.Name = Name;
			this.BindMatrix = BindMatrix;
		}

		public override string ToString() {
			return Name;
		}

		public void Read(BinaryReader Reader) {
			Name = Reader.ReadUTF8String();
			BindMatrix = Reader.ReadStruct<Matrix4x4>();
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteUTF8String(Name);
			Writer.WriteStruct(BindMatrix);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamBoneInfo {
		public int Bone1;
		public int Bone2;
		public int Bone3;
		public int Bone4;

		public float Weight1;
		public float Weight2;
		public float Weight3;
		public float Weight4;
	}

	[Flags]
	public enum FoamTextureType : int {
		Unknown,
		Diffuse,
		Normal,
		Specular,
		Glow,
		Reflection,
		Height,
		LightMap,
		Displacement,
		Ambient,
		Opacity
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct FoamTexture : IFoam {
		public string Name;
		public FoamTextureType Type;

		public FoamTexture(string Name, FoamTextureType Type) {
			this.Name = Name;
			this.Type = Type;
		}

		public void Read(BinaryReader Reader) {
			Name = Reader.ReadUTF8String();
			Type = (FoamTextureType)Reader.ReadInt32();
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteUTF8String(Name);
			Writer.Write((int)Type);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct FoamMaterial : IFoam {
		public string MaterialName;
		public FoamTexture[] Textures;

		public FoamMaterial(string MaterialName, FoamTexture[] Textures = null) {
			this.MaterialName = MaterialName;
			this.Textures = Textures;
		}

		public void AddTexture(FoamTexture Tex) {
			Utils.Append(ref Textures, Tex);
		}

		public bool FindTexture(FoamTextureType Type, out FoamTexture Tex) {
			for (int i = 0; i < Textures.Length; i++)
				if (Textures[i].Type == Type) {
					Tex = Textures[i];
					return true;
				}

			Tex = new FoamTexture();
			return false;
		}

		public void Read(BinaryReader Reader) {
			MaterialName = Reader.ReadUTF8String();

			int TexCount = Reader.ReadInt32();
			Textures = new FoamTexture[TexCount];

			for (int i = 0; i < TexCount; i++)
				Textures[i].Read(Reader);
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteUTF8String(MaterialName);

			int TexLen = 0;
			Writer.Write(TexLen = (Textures?.Length ?? 0));

			for (int i = 0; i < TexLen; i++)
				Textures[i].Write(Writer);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe class FoamMesh : IFoam {
		public FoamVertex3[] Vertices;
		public ushort[] Indices;

		public FoamBoneInfo[] BoneInformation;
		public FoamBone[] Bones;

		public string MeshName;
		public FoamMaterial Material;

		public FoamMesh() {
		}

		public FoamMesh(FoamVertex3[] Vertices, ushort[] Indices, FoamBoneInfo[] BoneInformation, FoamBone[] Bones, string MeshName, FoamMaterial Material) : this() {
			this.Vertices = Vertices;
			this.Indices = Indices;

			this.BoneInformation = BoneInformation;
			this.Bones = Bones;

			this.MeshName = MeshName;
			this.Material = Material;
		}

		public FoamVertex3[] GetFlatVertices() {
			if (Indices == null)
				return Vertices;

			List<FoamVertex3> Verts = new List<FoamVertex3>();

			for (int i = 0; i < Indices.Length; i++)
				Verts.Add(Vertices[Indices[i]]);

			return Verts.ToArray();
		}

		public void CalcBounds(out Vector3 Min, out Vector3 Max) {
			Min = new Vector3(float.PositiveInfinity);
			Max = new Vector3(float.NegativeInfinity);

			foreach (var V in Vertices) {
				Min = Vector3.Min(Min, V.Position);
				Max = Vector3.Max(Max, V.Position);
			}
		}

		public override string ToString() {
			return string.Format("[{0}] {1}{2}", Vertices.Length, Material.MaterialName, Indices != null ? " (Indices)" : "");
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteStructArray(Vertices);
			if (Indices != null)
				Writer.WriteStructArray(Indices);
			else
				Writer.Write(0);

			if (BoneInformation != null)
				Writer.WriteStructArray(BoneInformation);
			else
				Writer.Write(0);

			if (Bones != null) {
				Writer.Write(Bones.Length);
				for (int i = 0; i < Bones.Length; i++)
					Bones[i].Write(Writer);
			} else
				Writer.Write(0);

			Writer.WriteUTF8String(MeshName);
			Material.Write(Writer);
		}

		public void Read(BinaryReader Reader) {
			Vertices = Reader.ReadStructArray<FoamVertex3>();
			Indices = Reader.ReadStructArray<ushort>();
			if (Indices.Length == 0)
				Indices = null;

			BoneInformation = Reader.ReadStructArray<FoamBoneInfo>();
			if (BoneInformation.Length == 0)
				BoneInformation = null;

			Bones = new FoamBone[Reader.ReadInt32()];
			for (int i = 0; i < Bones.Length; i++)
				Bones[i].Read(Reader);
			if (Bones.Length == 0)
				Bones = null;

			MeshName = Reader.ReadUTF8String();
			Material.Read(Reader);
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
		public Vector3 Normal;
		public Vector3 Tangent;
		public Vector2 UV;
		public Vector2 UV2;
		public FoamColor Color;

		public FoamVertex3(Vector3 Position, Vector2 UV, Vector2 UV2, Vector3 Normal, Vector3 Tangent, FoamColor Color) {
			this.Position = Position;
			this.UV = UV;
			this.UV2 = UV2;
			this.Normal = Normal;
			this.Tangent = Tangent;
			this.Color = Color;
		}

		public FoamVertex3(Vector3 Position, Vector2 UV) : this(Position, UV, Vector2.Zero, Vector3.Zero, Vector3.Zero, FoamColor.White) {
		}
	}

	[Flags]
	public enum FoamFlags : int {
		Static,
		Animated,
		Level,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamModel {
		const int MAGIC = 0x6D616F46;

		public int Magic;
		public int Version;
		public string Name;
		public FoamFlags Flags;
		public FoamMesh[] Meshes;

		public FoamModel(string Name, FoamFlags Flags, FoamMesh[] Meshes) {
			Magic = MAGIC;
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
				Meshes[i] = new FoamMesh();
				Meshes[i].Read(Reader);
			}
		}

		public void CalcBounds(out Vector3 Min, out Vector3 Max) {
			Min = new Vector3(float.PositiveInfinity);
			Max = new Vector3(float.NegativeInfinity);

			foreach (var Mesh in Meshes) {
				Mesh.CalcBounds(out Vector3 MeshMin, out Vector3 MeshMax);
				Min = Vector3.Min(Min, MeshMin);
				Max = Vector3.Max(Max, MeshMax);
			}
		}

		public void SaveToFile(string FileName) {
			using (FileStream FS = File.Open(FileName, FileMode.Create))
			using (BinaryWriter Writer = new BinaryWriter(FS))
				Write(Writer);
		}

		public static FoamModel FromFile(string FileName) {
			FoamModel Header = new FoamModel();

			using (FileStream FS = File.OpenRead(FileName))
			using (BinaryReader Reader = new BinaryReader(FS))
				Header.Read(Reader);

			if (Header.Magic != MAGIC)
				throw new Exception("Invalid foam file " + FileName);

			return Header;
		}
	}
}
