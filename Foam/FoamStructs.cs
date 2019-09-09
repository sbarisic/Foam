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

	public struct FoamAnimationFrame : IFoam {
		public Matrix4x4[] BoneTransforms;

		public FoamAnimationFrame(Matrix4x4[] BoneTransforms) {
			this.BoneTransforms = BoneTransforms;
		}

		public void Read(BinaryReader Reader) {
			BoneTransforms = Reader.ReadStructArray<Matrix4x4>();
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteStructArray(BoneTransforms);
		}
	}

	public class FoamAnimation : IFoam {
		public string Name;
		public string[] BoneNames;

		public FoamAnimationFrame[] Frames;

		public float DurationInTicks;
		public float TicksPerSecond;


		public FoamAnimation() {
		}

		public FoamAnimation(string Name, FoamAnimationFrame[] Frames, string[] BoneNames, float DurationInTicks, float TicksPerSecond) {
			this.Name = Name;
			this.BoneNames = BoneNames;
			this.Frames = Frames;
			this.DurationInTicks = DurationInTicks;
			this.TicksPerSecond = TicksPerSecond;
		}

		public Matrix4x4 FindBoneTransform(string BoneName, int Frame) {
			for (int i = 0; i < BoneNames.Length; i++)
				if (BoneNames[i] == BoneName)
					return Frames[Frame].BoneTransforms[i];

			throw new Exception("Bone not found " + BoneName);
		}

		public void Read(BinaryReader Reader) {
			Name = Reader.ReadUTF8String();

			BoneNames = new string[Reader.ReadInt32()];
			for (int i = 0; i < BoneNames.Length; i++)
				BoneNames[i] = Reader.ReadUTF8String();
			if (BoneNames.Length == 0)
				BoneNames = null;

			Frames = new FoamAnimationFrame[Reader.ReadInt32()];
			for (int i = 0; i < Frames.Length; i++)
				Frames[i].Read(Reader);
			if (Frames.Length == 0)
				Frames = null;

			DurationInTicks = Reader.ReadSingle();
			TicksPerSecond = Reader.ReadSingle();
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteUTF8String(Name);

			if (BoneNames != null) {
				Writer.Write(BoneNames.Length);
				for (int i = 0; i < BoneNames.Length; i++)
					Writer.WriteUTF8String(BoneNames[i]);
			} else
				Writer.Write(0);

			if (Frames != null) {
				Writer.Write(Frames.Length);
				for (int i = 0; i < Frames.Length; i++)
					Frames[i].Write(Writer);
			} else
				Writer.Write(0);

			Writer.Write(DurationInTicks);
			Writer.Write(TicksPerSecond);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamBone : IFoam {
		public string Name;
		public int ParentBoneIndex;
		public Matrix4x4 BindMatrix;

		public FoamBone(string Name, int ParentBoneIndex, Matrix4x4 BindMatrix) {
			this.Name = Name;
			this.ParentBoneIndex = ParentBoneIndex;
			this.BindMatrix = BindMatrix;
		}

		public override string ToString() {
			return Name;
		}

		public void Read(BinaryReader Reader) {
			Name = Reader.ReadUTF8String();
			ParentBoneIndex = Reader.ReadInt32();
			BindMatrix = Reader.ReadStruct<Matrix4x4>();
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteUTF8String(Name);
			Writer.Write(ParentBoneIndex);
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

		public string MeshName;
		public int MaterialIndex;

		// Not serialized
		public object Userdata;

		public FoamMesh() {
		}

		public FoamMesh(FoamVertex3[] Vertices, ushort[] Indices, FoamBoneInfo[] BoneInformation, string MeshName, int MaterialIndex) : this() {
			this.Vertices = Vertices;
			this.Indices = Indices;

			this.BoneInformation = BoneInformation;

			this.MeshName = MeshName;
			this.MaterialIndex = MaterialIndex;
		}

		public FoamVertex3[] GetFlatVertices() {
			if (Indices == null)
				return Vertices;

			List<FoamVertex3> Verts = new List<FoamVertex3>();

			for (int i = 0; i < Indices.Length; i++) {
				ushort Index = Indices[i];
				Verts.Add(Vertices[Index]);
			}

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
			return string.Format("[{0}] {1}{2}", Vertices.Length, MaterialIndex, Indices != null ? " (Indices)" : "");
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

			Writer.WriteUTF8String(MeshName);
			Writer.Write(MaterialIndex);
		}

		public void Read(BinaryReader Reader) {
			Vertices = Reader.ReadStructArray<FoamVertex3>();
			Indices = Reader.ReadStructArray<ushort>();
			if (Indices.Length == 0)
				Indices = null;

			BoneInformation = Reader.ReadStructArray<FoamBoneInfo>();
			if (BoneInformation.Length == 0)
				BoneInformation = null;

			MeshName = Reader.ReadUTF8String();
			MaterialIndex = Reader.ReadInt32();
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

		public FoamColor(float R, float G, float B, float A) : this((byte)(R * 255), (byte)(G * 255), (byte)(B * 255), (byte)(A * 255)) {
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

	public enum FoamFlags : int {
		Model,
		FrameAnimatedModel,

		//Level,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FoamExtension : IFoam {
		public string Name;
		public byte[] Data;

		public FoamExtension(string Name, byte[] Data) {
			this.Name = Name;
			this.Data = Data;
		}

		public void Read(BinaryReader Reader) {
			Name = Reader.ReadUTF8String();
			Data = Reader.ReadStructArray<byte>();
		}

		public void Write(BinaryWriter Writer) {
			Writer.WriteUTF8String(Name);
			Writer.WriteStructArray(Data);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class FoamModel {
		const int MAGIC = 0x6D616F46;
		const int VERSION = 1;

		public int Magic;
		public int Version;
		public string Name;
		public FoamFlags Flags;

		public FoamMesh[] Meshes;
		public FoamBone[] Bones;
		public FoamAnimation[] Animations;
		public FoamMaterial[] Materials;
		public FoamExtension[] Extensions;

		public FoamModel() {
		}

		public FoamModel(string Name, FoamFlags Flags, FoamMesh[] Meshes, FoamBone[] Bones, FoamAnimation[] Animations, FoamMaterial[] Materials) {
			Magic = MAGIC;
			Version = VERSION;
			this.Name = Name;
			this.Flags = Flags;
			this.Meshes = Meshes;
			this.Bones = Bones;
			this.Animations = Animations;
			this.Materials = Materials;
		}

		public void Write(BinaryWriter Writer) {
			Writer.Write(Magic);
			Writer.Write(Version);
			Writer.WriteUTF8String(Name);
			Writer.Write((int)Flags);

			Writer.Write(Meshes.Length);
			for (int i = 0; i < Meshes.Length; i++)
				Meshes[i].Write(Writer);

			// Bones
			if (Bones != null) {
				Writer.Write(Bones.Length);
				for (int i = 0; i < Bones.Length; i++)
					Bones[i].Write(Writer);
			} else
				Writer.Write(0);

			// Animations
			if (Animations != null) {
				Writer.Write(Animations.Length);
				for (int i = 0; i < Animations.Length; i++)
					Animations[i].Write(Writer);
			} else
				Writer.Write(0);

			// Materials
			if (Materials != null) {
				Writer.Write(Materials.Length);
				for (int i = 0; i < Materials.Length; i++)
					Materials[i].Write(Writer);
			} else
				Writer.Write(0);

			// Extensions
			if (Extensions != null) {
				Writer.Write(Extensions.Length);
				for (int i = 0; i < Extensions.Length; i++)
					Extensions[i].Write(Writer);
			} else
				Writer.Write(0);
		}

		public void Read(BinaryReader Reader) {
			Magic = Reader.ReadInt32();
			if (Magic != MAGIC)
				throw new Exception("Invalid foam file ");

			Version = Reader.ReadInt32();
			if (Version != VERSION)
				throw new Exception("Unsupported foam version " + Version);

			Name = Reader.ReadUTF8String();
			Flags = (FoamFlags)Reader.ReadInt32();

			int MeshCount = Reader.ReadInt32();
			Meshes = new FoamMesh[MeshCount];

			for (int i = 0; i < MeshCount; i++) {
				Meshes[i] = new FoamMesh();
				Meshes[i].Read(Reader);
			}

			// Bones
			Bones = new FoamBone[Reader.ReadInt32()];
			for (int i = 0; i < Bones.Length; i++)
				Bones[i].Read(Reader);
			if (Bones.Length == 0)
				Bones = null;

			// Animations
			Animations = new FoamAnimation[Reader.ReadInt32()];
			for (int i = 0; i < Animations.Length; i++) {
				Animations[i] = new FoamAnimation();
				Animations[i].Read(Reader);
			}
			if (Animations.Length == 0)
				Animations = null;

			// Materials
			Materials = new FoamMaterial[Reader.ReadInt32()];
			for (int i = 0; i < Materials.Length; i++)
				Materials[i].Read(Reader);
			if (Materials.Length == 0)
				Materials = null;

			// Extensions
			Extensions = new FoamExtension[Reader.ReadInt32()];
			for (int i = 0; i < Extensions.Length; i++)
				Extensions[i].Read(Reader);
			if (Extensions.Length == 0)
				Extensions = null;
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

		public Matrix4x4 CalcWorldTransform(int AnimIndex, int FrameIndex, int BoneIndex) {
			FoamBone CurBone = Bones[BoneIndex];
			Matrix4x4 ParentTrans = Matrix4x4.Identity;

			if (CurBone.ParentBoneIndex != -1)
				ParentTrans = CalcWorldTransform(AnimIndex, FrameIndex, CurBone.ParentBoneIndex);

			return Animations[AnimIndex].FindBoneTransform(CurBone.Name, FrameIndex) * ParentTrans;
		}

		public Matrix4x4 CalcBindTransform(int BoneIndex) {
			Matrix4x4.Invert(Bones[BoneIndex].BindMatrix, out Matrix4x4 BindWorldInv);
			return BindWorldInv;
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

			return Header;
		}
	}
}
