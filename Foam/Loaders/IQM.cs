using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// https://www.icculus.org/homepages/phaethon/q3a/formats/md3format.html
// https://github.com/excessive/iqm-exm/blob/master/iqm.txt
// https://github.com/lsalzman/iqm/tree/master/demo
// https://github.com/lsalzman/iqm/blob/master/demo/demo.cpp


namespace Foam.Loaders {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMHeader {
		fixed byte magic[16];// the string "INTERQUAKEMODEL\0", 0 terminated
		public uint Version; // must be version 2
		public uint filesize;
		public uint flags;
		public uint num_text, ofs_text;
		public uint num_meshes, ofs_meshes;
		public uint num_vertexarrays, num_vertexes, ofs_vertexarrays;
		public uint num_triangles, ofs_triangles, ofs_adjacency;
		public uint num_joints, ofs_joints;
		public uint num_poses, ofs_poses;
		public uint num_anims, ofs_anims;
		public uint num_frames, num_framechannels, ofs_frames, ofs_bounds;
		public uint num_comment, ofs_comment;
		public uint num_extensions, ofs_extensions; // these are stored as a linked list, not as a contiguous array

		public string GetMagic() {
			fixed (byte* magicptr = magic)
				return Marshal.PtrToStringAnsi(new IntPtr(magicptr));
		}
	}

	// ofs_* fields are relative to the beginning of the iqmheader struct
	// ofs_* fields must be set to 0 when the particular data is empty
	// ofs_* fields must be aligned to at least 4 byte boundaries

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct IQMMesh {
		public uint name;
		public uint material;
		public uint first_vertex, num_vertexes;
		public uint first_triangle, num_triangles;
	}

	// all vertex array entries must ordered as defined below, if present
	// i.e. position comes before normal comes before ... comes before custom
	// where a format and size is given, this means models intended for portable use should use these
	// an IQM implementation is not required to honor any other format/size than those recommended
	// however, it may support other format/size combinations for these types if it desires.
	// vertex positions use the same coordinate system as Blender, where:
	// X = right (east), Y = forward (north), Z = up.
	// 1 unit = 1 meter.
	public enum IQMVertexArrayType {
		IQM_POSITION = 0,  // float, 3
		IQM_TEXCOORD = 1,  // float, 2
		IQM_NORMAL = 2,  // float, 3
		IQM_TANGENT = 3,  // float, 4
		IQM_BLENDINDEXES = 4,  // ubyte, 4
		IQM_BLENDWEIGHTS = 5,  // ubyte, 4
		IQM_COLOR = 6,  // ubyte, 4

		// all values up to IQM_CUSTOM are reserved for future use
		// any value >= IQM_CUSTOM is interpreted as CUSTOM type
		// the value then defines an offset into the string table, where offset = value - IQM_CUSTOM
		// this must be a valid string naming the type
		IQM_CUSTOM = 0x10
	}

	public enum IQMVertexArrayFormat : uint {
		IQM_BYTE = 0,
		IQM_UBYTE = 1,
		IQM_SHORT = 2,
		IQM_USHORT = 3,
		IQM_INT = 4,
		IQM_UINT = 5,
		IQM_HALF = 6,
		IQM_FLOAT = 7,
		IQM_DOUBLE = 8,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct IQMVertexArray {
		public IQMVertexArrayType type;   // type or custom name
		public uint flags;
		public IQMVertexArrayFormat format; // component format
		public uint size;   // number of components
		public uint offset; // offset to array of tightly packed components, with num_vertexes * size total entries
							// offset must be aligned to max(sizeof(format), 4)
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMTriangle {
		// triangle vertex indices, clockwise winding.
		// public fixed uint vertex[3];

		public uint Vertex1;
		public uint Vertex2;
		public uint Vertex3;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMAdjacency {
		// each value is the index of the adjacent triangle for edge 0, 1, and 2, where ~0 (= -1) indicates no adjacent triangle
		// indexes are relative to the iqmheader.ofs_triangles array and span all meshes, where 0 is the first triangle, 1 is the second, 2 is the third, etc. 
		//public fixed uint triangle[3];

		public uint Triangle1;
		public uint Triangle2;
		public uint Triangle3;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMJoint {
		public uint name;
		public int parent; // parent < 0 means this is a root bone

		public Vector3 Translate;
		public Quaternion Rotate;
		public Vector3 Scale;

		//public fixed float translate[3], rotate[4], scale[3];
		// translate is translation <Tx, Ty, Tz>, and rotate is quaternion rotation <Qx, Qy, Qz, Qw>
		// rotation is in relative/parent local space
		// scale is pre-scaling <Sx, Sy, Sz>
		// output = (input*scale)*rotation + translation
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMPose {
		public int parent; // parent < 0 means this is a root bone
		public uint channelmask; // mask of which 10 channels are present for this joint pose


		//public fixed float channeloffset[10], channelscale[10];
		// channels 0..2 are translation <Tx, Ty, Tz> and channels 3..6 are quaternion rotation <Qx, Qy, Qz, Qw>
		// rotation is in relative/parent local space
		// channels 7..9 are scale <Sx, Sy, Sz>
		// output = (input*scale)*rotation + translation

		public Vector3 Translate;
		public Quaternion Rotate;
		public Vector3 Scale;

		public Vector3 TranslateScale;
		public Vector4 RotateScale;
		public Vector3 ScaleScale;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMAnim {
		public uint name;
		public uint first_frame, num_frames;
		public float framerate;
		public uint flags;
	};

	enum IQMAnimFlags {
		IQM_LOOP = 1 << 0
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMExtension {
		public uint name;
		public uint num_data, ofs_data;
		public uint ofs_extensions; // pointer to next extension
	};

	// vertex data is not really interleaved, but this just gives examples of standard types of the data arrays
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMVertex {
		//public fixed float position[3], texcoord[2], normal[3], tangent[4];
		//public fixed byte blendindices[4], blendweights[4], color[4];

		public Vector3 position;
		public Vector2 texcoord;
		public Vector3 normal;
		public Vector4 tangent;

		public fixed byte blendindices[4];
		public fixed byte blendweights[4];

		public FoamColor color;

	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMBounds {
		public Vector3 BBMins;
		public Vector3 BBMaxs;// the minimum and maximum coordinates of the bounding box for this animation frame

		// public fixed float bbmins[3], bbmaxs[3]; 
		public float xyradius, radius; // the circular radius in the X-Y plane, as well as the spherical radius
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMBlendIndices {
		public byte BlendIndex1;
		public byte BlendIndex2;
		public byte BlendIndex3;
		public byte BlendIndex4;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct IQMBlendWeights {
		public byte BlendWeight1;
		public byte BlendWeight2;
		public byte BlendWeight3;
		public byte BlendWeight4;
	}

	public unsafe static class IQM {
		public static bool ConvertToCCW = true;

		static Vector3[] Position;
		static Vector2[] Texcoord;
		static Vector3[] Normal;
		static Vector4[] Tangent;
		static IQMBlendIndices[] BlendIndexes;
		static IQMBlendWeights[] BlendWeights;
		static FoamColor[] Color;
		static byte[] Text;
		static byte[] Comment;
		static IQMTriangle[] Triangles;
		static IQMMesh[] Meshes;
		static IQMJoint[] Joints;
		static IQMPose[] Poses;
		static IQMAnim[] Anims;

		public static FoamModel Load(string IQMFile) {
			int NumFrames = 0;
			ushort[] FrameData = null;

			Position = null;
			Texcoord = null;
			Normal = null;
			Tangent = null;
			BlendIndexes = null;
			BlendWeights = null;
			Color = null;
			Text = null;
			Comment = null;
			Triangles = null;
			Meshes = null;
			Joints = null;
			Poses = null;
			Anims = null;

			FoamVertex3[] FoamVertices = null;
			FoamBoneInfo[] FoamBoneInfo = null;

			using (FileStream Stream = File.OpenRead(IQMFile))
			using (BinaryReader Reader = new BinaryReader(Stream)) {
				IQMHeader Header = Reader.ReadStruct<IQMHeader>();
				NumFrames = (int)Header.num_frames;

				if (Header.GetMagic() != "INTERQUAKEMODEL")
					throw new Exception("Invalid magic in IQM file");

				if (Header.Version != 2)
					throw new Exception("Only IQM version 2 supported");

				// Text
				if (Header.ofs_text != 0) {
					Reader.Seek(Header.ofs_text);
					//Text = Encoding.ASCII.GetString(Reader.ReadBytes((int)Header.num_text)).Split(new char[] { (char)0 });
					Text = Reader.ReadBytes((int)Header.num_text);
				}

				// Comments
				if (Header.ofs_comment != 0) {
					Reader.Seek(Header.ofs_comment);
					Comment = Reader.ReadBytes((int)Header.num_comment);
				}

				// Vertex arrays
				Reader.Seek(Header.ofs_vertexarrays);
				IQMVertexArray[] VertArrays = new IQMVertexArray[Header.num_vertexarrays];
				for (int i = 0; i < VertArrays.Length; i++)
					VertArrays[i] = Reader.ReadStruct<IQMVertexArray>();

				for (int i = 0; i < VertArrays.Length; i++) {
					ref IQMVertexArray VA = ref VertArrays[i];
					Reader.Seek(VA.offset);

					switch (VA.type) {
						case IQMVertexArrayType.IQM_POSITION:
							if (VA.format != IQMVertexArrayFormat.IQM_FLOAT && VA.size != 3)
								throw new NotImplementedException();

							Position = Reader.ReadStructArray<Vector3>((uint)(Header.num_vertexes * Marshal.SizeOf<Vector3>()));
							break;

						case IQMVertexArrayType.IQM_TEXCOORD:
							if (VA.format != IQMVertexArrayFormat.IQM_FLOAT && VA.size != 2)
								throw new NotImplementedException();

							Texcoord = Reader.ReadStructArray<Vector2>((uint)(Header.num_vertexes * Marshal.SizeOf<Vector2>()));
							break;

						case IQMVertexArrayType.IQM_NORMAL:
							if (VA.format != IQMVertexArrayFormat.IQM_FLOAT && VA.size != 3)
								throw new NotImplementedException();

							Normal = Reader.ReadStructArray<Vector3>((uint)(Header.num_vertexes * Marshal.SizeOf<Vector3>()));
							break;

						case IQMVertexArrayType.IQM_TANGENT:
							if (VA.format != IQMVertexArrayFormat.IQM_FLOAT && VA.size != 4)
								throw new NotImplementedException();

							Tangent = Reader.ReadStructArray<Vector4>((uint)(Header.num_vertexes * Marshal.SizeOf<Vector4>()));
							break;

						case IQMVertexArrayType.IQM_BLENDINDEXES:
							if (VA.format != IQMVertexArrayFormat.IQM_UBYTE && VA.size != 4)
								throw new NotImplementedException();

							BlendIndexes = Reader.ReadStructArray<IQMBlendIndices>((uint)(Header.num_vertexes * Marshal.SizeOf<IQMBlendIndices>()));
							break;

						case IQMVertexArrayType.IQM_BLENDWEIGHTS:
							if (VA.format != IQMVertexArrayFormat.IQM_UBYTE && VA.size != 4)
								throw new NotImplementedException();

							BlendWeights = Reader.ReadStructArray<IQMBlendWeights>((uint)(Header.num_vertexes * Marshal.SizeOf<IQMBlendWeights>()));
							break;

						case IQMVertexArrayType.IQM_COLOR:
							if (VA.format != IQMVertexArrayFormat.IQM_UBYTE && VA.size != 4)
								throw new NotImplementedException();

							Color = Reader.ReadStructArray<FoamColor>((uint)(Header.num_vertexes * Marshal.SizeOf<FoamColor>()));
							break;

						case IQMVertexArrayType.IQM_CUSTOM:
						default:
							throw new NotImplementedException();
					}
				}

				// Triangles
				Reader.Seek(Header.ofs_triangles);
				Triangles = Reader.ReadStructArray<IQMTriangle>((uint)(Header.num_triangles * sizeof(IQMTriangle)));

				// Meshes
				Reader.Seek(Header.ofs_meshes);
				Meshes = Reader.ReadStructArray<IQMMesh>((uint)(Header.num_meshes * sizeof(IQMMesh)));

				// Joints
				Reader.Seek(Header.ofs_joints);
				Joints = Reader.ReadStructArray<IQMJoint>((uint)(Header.num_joints * sizeof(IQMJoint)));

				// Poses
				Reader.Seek(Header.ofs_poses);
				Poses = Reader.ReadStructArray<IQMPose>((uint)(Header.num_poses * sizeof(IQMPose)));

				// Anims
				Reader.Seek(Header.ofs_anims);
				Anims = Reader.ReadStructArray<IQMAnim>((uint)(Header.num_anims * sizeof(IQMAnim)));

				// Frames
				Reader.Seek(Header.ofs_frames);
				FrameData = Reader.ReadStructArray<ushort>((uint)(CountFrameDataLength(NumFrames) * sizeof(ushort)));

				// Foam vertices
				FoamVertices = new FoamVertex3[Header.num_vertexes];
				for (int i = 0; i < FoamVertices.Length; i++)
					FoamVertices[i] = BuildVertex(i);

				// Foam bone info
				FoamBoneInfo = new FoamBoneInfo[FoamVertices.Length];
				for (int i = 0; i < FoamBoneInfo.Length; i++) {
					FoamBoneInfo Info = new FoamBoneInfo();
					IQMBlendIndices BInd = BlendIndexes[i];
					IQMBlendWeights BWgt = BlendWeights[i];

					Info.Bone1 = BInd.BlendIndex1;
					Info.Bone2 = BInd.BlendIndex2;
					Info.Bone3 = BInd.BlendIndex3;
					Info.Bone4 = BInd.BlendIndex4;

					const float WeightDividend = 255.0f;
					Info.Weight1 = BWgt.BlendWeight1 / WeightDividend;
					Info.Weight2 = BWgt.BlendWeight2 / WeightDividend;
					Info.Weight3 = BWgt.BlendWeight3 / WeightDividend;
					Info.Weight4 = BWgt.BlendWeight4 / WeightDividend;

					FoamBoneInfo[i] = Info;
				}
			}

			FoamBone[] FoamBones = new FoamBone[Joints.Length];

			List<FoamAnimationFrame> FoamFrames = new List<FoamAnimationFrame>();
			List<string> FoamFrameBoneNames = new List<string>();

			// Joints
			for (int i = 0; i < Joints.Length; i++) {
				ref IQMJoint J = ref Joints[i];

				Matrix4x4 JointMatrix = CreateMatrix(J.Translate, J.Rotate, J.Scale);
				Matrix4x4.Invert(JointMatrix, out Matrix4x4 JointMatrixInv);
				FoamBones[i] = new FoamBone(GetText(J.name), J.parent, JointMatrixInv);
			}

			for (int i = 0; i < FoamBones.Length; i++)
				if (FoamBones[i].ParentBoneIndex >= 0)
					FoamBones[i].BindMatrix = FoamBones[FoamBones[i].ParentBoneIndex].BindMatrix * FoamBones[i].BindMatrix;

			// Frames
			int FrameIdx = 0;
			for (int i = 0; i < NumFrames; i++) {
				FoamAnimationFrame FoamFrame = new FoamAnimationFrame(new Matrix4x4[Poses.Length]);

				for (int j = 0; j < Poses.Length; j++) {
					ref IQMPose P = ref Poses[j];

					Vector3 Translate = P.Translate;
					Quaternion Rotate = P.Rotate;
					Vector3 Scale = P.Scale;

					if ((P.channelmask & 0x01) != 0)
						Translate.X += FrameData[FrameIdx++] * P.TranslateScale.X;
					if ((P.channelmask & 0x02) != 0)
						Translate.Y += FrameData[FrameIdx++] * P.TranslateScale.Y;
					if ((P.channelmask & 0x04) != 0)
						Translate.Z += FrameData[FrameIdx++] * P.TranslateScale.Z;

					if ((P.channelmask & 0x08) != 0)
						Rotate.X += FrameData[FrameIdx++] * P.RotateScale.X;
					if ((P.channelmask & 0x10) != 0)
						Rotate.Y += FrameData[FrameIdx++] * P.RotateScale.Y;
					if ((P.channelmask & 0x20) != 0)
						Rotate.Z += FrameData[FrameIdx++] * P.RotateScale.Z;
					if ((P.channelmask & 0x40) != 0)
						Rotate.W += FrameData[FrameIdx++] * P.RotateScale.W;

					if ((P.channelmask & 0x80) != 0)
						Scale.X += FrameData[FrameIdx++] * P.ScaleScale.X;
					if ((P.channelmask & 0x100) != 0)
						Scale.Y += FrameData[FrameIdx++] * P.ScaleScale.Y;
					if ((P.channelmask & 0x200) != 0)
						Scale.Z += FrameData[FrameIdx++] * P.ScaleScale.Z;

					if (i == 0)
						FoamFrameBoneNames.Add(FoamBones[j].Name);

					FoamFrame.BoneTransforms[j] = CreateMatrix(Translate, Rotate, Scale, false);
				}

				FoamFrames.Add(FoamFrame);
			}

			// Foam animations
			FoamAnimation[] FoamAnims = Anims.Length > 0 ? new FoamAnimation[Anims.Length] : null;
			for (int i = 0; i < Anims.Length; i++) {
				ref IQMAnim Anim = ref Anims[i];
				FoamAnims[i] = new FoamAnimation(GetText(Anim.name), FoamFrames.ToArray(), FoamFrameBoneNames.ToArray(), Anim.num_frames, Anim.framerate);
			}

			List<FoamMaterial> FoamMaterials = new List<FoamMaterial>();
			List<FoamMesh> FoamMeshes = new List<FoamMesh>();
			foreach (var M in Meshes)
				FoamMeshes.Add(BuildFoamMesh(M, FoamVertices, FoamBoneInfo, FoamMaterials));

			//return BuildFoamModel(Path.GetFileNameWithoutExtension(IQMFile));
			return new FoamModel(Path.GetFileNameWithoutExtension(IQMFile), FoamFlags.Model, FoamMeshes.ToArray(), FoamBones, FoamAnims, FoamMaterials.ToArray());
		}

		static int CountFrameDataLength(int NumFrames) {
			int FrameCount = 0;

			for (int i = 0; i < NumFrames; i++) {
				for (int j = 0; j < Poses.Length; j++) {
					ref IQMPose P = ref Poses[j];

					if ((P.channelmask & 0x01) != 0)
						FrameCount++;
					if ((P.channelmask & 0x02) != 0)
						FrameCount++;
					if ((P.channelmask & 0x04) != 0)
						FrameCount++;

					if ((P.channelmask & 0x08) != 0)
						FrameCount++;
					if ((P.channelmask & 0x10) != 0)
						FrameCount++;
					if ((P.channelmask & 0x20) != 0)
						FrameCount++;
					if ((P.channelmask & 0x40) != 0)
						FrameCount++;

					if ((P.channelmask & 0x80) != 0)
						FrameCount++;
					if ((P.channelmask & 0x100) != 0)
						FrameCount++;
					if ((P.channelmask & 0x200) != 0)
						FrameCount++;
				}
			}

			return FrameCount;
		}

		static string GetText(uint Idx) {
			int Len = 0;

			while (Text[Idx + Len] != 0)
				Len++;

			return Encoding.UTF8.GetString(Text, (int)Idx, Len);
		}

		static FoamVertex3 BuildVertex(int Idx) {
			Vector4 Tgt = Tangent?[Idx] ?? Vector4.Zero;

			Vector3 _Position = Position[Idx];
			Vector2 _UV = Texcoord?[Idx] ?? Vector2.Zero;
			Vector3 _Normal = Normal?[Idx] ?? Vector3.Zero;
			Vector3 _Tangent = new Vector3(Tgt.X, Tgt.Y, Tgt.Z);
			FoamColor _Color = Color?[Idx] ?? FoamColor.White;

			if (ConvertToCCW) {
				_UV = new Vector2(_UV.X, 1 - _UV.Y);
				_Normal = SwapYZ(_Normal);
				_Tangent = SwapYZ(_Tangent);
			}

			return new FoamVertex3(_Position, _UV, Vector2.Zero, _Normal, _Tangent, _Color);
		}

		static Vector3 SwapYZ(Vector3 V) {
			float Z = V.Z;
			V.Z = V.Y;
			V.Y = Z;
			return V;
		}

		static Matrix4x4 CreateMatrix(Vector3 Translate, Quaternion Rotate, Vector3 Scale, bool NormalizeRotation = true) {
			if (NormalizeRotation)
				Rotate = Quaternion.Normalize(Rotate);

			return Utils.CreateMatrix(Translate, Rotate, Scale);
		}

		static FoamMesh BuildFoamMesh(IQMMesh IQMMesh, FoamVertex3[] Vertices, FoamBoneInfo[] BoneInfo, List<FoamMaterial> FoamMaterials) {
			List<ushort> FoamIndices = new List<ushort>();

			int FirstTri = (int)IQMMesh.first_triangle;
			for (int i = 0; i < IQMMesh.num_triangles; i++) {
				ushort Vert1 = (ushort)(Triangles[FirstTri + i].Vertex1);
				ushort Vert2 = (ushort)(Triangles[FirstTri + i].Vertex2);
				ushort Vert3 = (ushort)(Triangles[FirstTri + i].Vertex3);

				if (ConvertToCCW) {
					FoamIndices.Add(Vert3);
					FoamIndices.Add(Vert2);
					FoamIndices.Add(Vert1);
				} else {
					FoamIndices.Add(Vert1);
					FoamIndices.Add(Vert2);
					FoamIndices.Add(Vert3);
				}
			}

			ushort MinIndex = FoamIndices.Min();
			ushort MaxIndex = FoamIndices.Max();
			int Count = (MaxIndex - MinIndex) + 1;

			FoamVertex3[] FoamVertices = Vertices.Skip(MinIndex).Take(Count).ToArray();
			FoamBoneInfo[] FoamBoneInfo = BoneInfo.Skip(MinIndex).Take(Count).ToArray();
			string MeshName = GetText(IQMMesh.name);
			string MaterialName = GetText(IQMMesh.material);

			FoamMaterials.Add(new FoamMaterial(MaterialName, new[] { new FoamTexture(MaterialName, FoamTextureType.Diffuse) }));
			return new FoamMesh(FoamVertices, FoamIndices.Select(I => (ushort)(I - MinIndex)).ToArray(), FoamBoneInfo, MeshName, FoamMaterials.Count - 1);
		}
	}
}