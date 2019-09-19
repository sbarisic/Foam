using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Foam {
	public static unsafe class Utils {
		public static float ParseFloat(this string Str, float Default) {
			if (string.IsNullOrWhiteSpace(Str))
				return Default;

			return Str.ParseFloat();
		}

		public static float ParseFloat(this string Str) {
			return float.Parse(Str, CultureInfo.InvariantCulture);
		}

		public static int ParseInt(this string Str, int Default) {
			if (string.IsNullOrWhiteSpace(Str))
				return Default;

			return Str.ParseInt();
		}

		public static int ParseInt(this string Str) {
			return int.Parse(Str, CultureInfo.InvariantCulture);
		}

		public static void WriteUTF8String(this BinaryWriter Writer, string Str) {
			byte[] StringBytes = Encoding.UTF8.GetBytes(Str);
			Writer.Write(StringBytes.Length);
			Writer.Write(StringBytes);
		}

		public static string ReadUTF8String(this BinaryReader Reader) {
			int Len = Reader.ReadInt32();
			byte[] StringBytes = Reader.ReadBytes(Len);
			return Encoding.UTF8.GetString(StringBytes);
		}

		public static string ReadNullTerminatedString(this BinaryReader Reader) {
			int Cur = Reader.GetOffset();

			int Len = 0;
			while (Reader.ReadByte() != 0)
				Len++;

			Reader.Seek(Cur);
			string Str = Encoding.UTF8.GetString(Reader.ReadBytes(Len));
			Reader.ReadByte(); // Null
			return Str;
		}

		public static void WriteStructArray<T>(this BinaryWriter Writer, T[] Arr) where T : unmanaged {
			Writer.Write(Arr.Length);
			int LenBytes = Arr.Length * sizeof(T);

			fixed (T* ArrPtr = Arr) {
				byte* BytePtr = (byte*)ArrPtr;

				for (int i = 0; i < LenBytes; i++)
					Writer.Write(BytePtr[i]);
			}
		}

		public static T[] ReadStructArrayReverse<T>(this BinaryReader Reader, int Count) where T : unmanaged {
			T[] Arr = new T[Count];

			for (int i = 0; i < Count; i++)
				Arr[i] = Reader.ReadStructReverse<T>();

			return Arr;
		}

		public static T[] ReadStructArray<T>(this BinaryReader Reader, uint LenBytes) where T : unmanaged {
			return Reader.ReadStructArray<T>((int)LenBytes);
		}

		public static T[] ReadStructArrayToEnd<T>(this BinaryReader Reader) where T : unmanaged {
			uint LenBytes = (uint)(Reader.BaseStream.Length - (long)Reader.GetOffset());
			return Reader.ReadStructArray<T>((int)LenBytes);
		}

		public static T[] ReadStructArray<T>(this BinaryReader Reader, int LenBytes) where T : unmanaged {
			T[] Arr = new T[LenBytes / sizeof(T)];

			fixed (T* ArrPtr = Arr) {
				byte* BytePtr = (byte*)ArrPtr;

				for (int i = 0; i < LenBytes; i++)
					BytePtr[i] = Reader.ReadByte();
			}

			return Arr;
		}

		public static T[] ReadStructArray<T>(this BinaryReader Reader) where T : unmanaged {
			return Reader.ReadStructArray<T>(Reader.ReadInt32() * sizeof(T));
		}

		public static void WriteStruct<T>(this BinaryWriter Writer, T Val) where T : unmanaged {
			byte* ValPtr = (byte*)&Val;

			for (int i = 0; i < sizeof(T); i++)
				Writer.Write(ValPtr[i]);
		}

		public static T ReadStruct<T>(this BinaryReader Reader) where T : unmanaged {
			T Val;
			byte* ValPtr = (byte*)&Val;

			for (int i = 0; i < sizeof(T); i++)
				ValPtr[i] = Reader.ReadByte();

			return Val;
		}

		public static T ReadStructReverse<T>(this BinaryReader Reader) where T : unmanaged {
			T Val;
			byte* ValPtr = (byte*)&Val;

			for (int i = sizeof(T) - 1; i >= 0; i--)
				ValPtr[i] = Reader.ReadByte();

			return Val;
		}

		public static byte[] ToByteArray(byte* Ptr, int Len) {
			byte[] Bytes = new byte[Len];

			for (int i = 0; i < Len; i++)
				Bytes[i] = Ptr[i];

			return Bytes;
		}

		public static void Append<T>(ref T[] Arr, T Val) {
			if (Arr == null)
				Arr = new T[0];

			int NewLen = Arr.Length + 1;
			Array.Resize(ref Arr, NewLen);
			Arr[NewLen - 1] = Val;
		}

		public static void Prepend<T>(ref T[] Arr, T Val) {
			if (Arr == null)
				Arr = new T[0];

			int NewLen = Arr.Length + 1;
			Array.Resize(ref Arr, NewLen);

			for (int i = NewLen - 1; i >= 1; i--)
				Arr[i] = Arr[i - 1];

			Arr[0] = Val;
		}

		public static float Max(Vector3 V) {
			return Math.Max(Math.Max(V.X, V.Y), V.Z);
		}

		public static float Min(Vector3 V) {
			return Math.Min(Math.Min(V.X, V.Y), V.Z);
		}

		public static Vector2 Round(Vector2 V) {
			return new Vector2((int)Math.Round(V.X), (int)Math.Round(V.Y));
		}

		public static Vector2 Floor(Vector2 V) {
			return new Vector2((int)Math.Floor(V.X), (int)Math.Floor(V.Y));
		}

		public static Vector2 Min(Vector2 A, Vector2 B) {
			return new Vector2(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y));
		}

		public static Vector2 Max(Vector2 A, Vector2 B) {
			return new Vector2(Math.Max(A.X, B.X), Math.Max(A.Y, B.Y));
		}

		public static Vector3 Min(Vector3 A, Vector3 B) {
			return new Vector3(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y), Math.Min(A.Z, B.Z));
		}

		public static Vector3 Max(Vector3 A, Vector3 B) {
			return new Vector3(Math.Max(A.X, B.X), Math.Max(A.Y, B.Y), Math.Max(A.Z, B.Z));
		}

		public static Vector4 Min(Vector4 A, Vector4 B) {
			return new Vector4(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y), Math.Min(A.Z, B.Z), Math.Min(A.W, B.W));
		}

		public static Vector4 Max(Vector4 A, Vector4 B) {
			return new Vector4(Math.Max(A.X, B.X), Math.Max(A.Y, B.Y), Math.Max(A.Z, B.Z), Math.Max(A.W, B.W));
		}

		public static int GetOffset(this BinaryReader Reader) {
			return (int)Reader.BaseStream.Position;
		}

		public static int Seek(this BinaryReader Reader, uint AbsOffset) {
			int Cur = Reader.GetOffset();
			Reader.BaseStream.Seek(AbsOffset, SeekOrigin.Begin);
			return Cur;
		}

		public static int Seek(this BinaryReader Reader, int AbsOffset) {
			return Reader.Seek((uint)AbsOffset);
		}

		public static Quaternion ToQuat(this Vector4 V) {
			return new Quaternion(V.X, V.Y, V.Z, V.W);
		}

		public static Vector4 ToVec(this Quaternion Q) {
			return new Vector4(Q.X, Q.Y, Q.Z, Q.W);
		}

		public static float Lerp(float A, float B, float Amt) {
			return A * (1 - Amt) + B * Amt;
		}

		public static Vector3 Lerp(Vector3 A, Vector3 B, float Amt) {
			return new Vector3(Lerp(A.X, B.X, Amt), Lerp(A.Y, B.Y, Amt), Lerp(A.Z, B.Z, Amt));
		}

		public static Vector4 Lerp(Vector4 A, Vector4 B, float Amt) {
			return new Vector4(Lerp(A.X, B.X, Amt), Lerp(A.Y, B.Y, Amt), Lerp(A.Z, B.Z, Amt), Lerp(A.W, B.W, Amt));
		}

		public static Matrix4x4 Lerp(Matrix4x4 A, Matrix4x4 B, float Amt) {
			Matrix4x4.Decompose(A, out Vector3 A_Scale, out Quaternion A_Rot, out Vector3 A_Trans);
			Matrix4x4.Decompose(B, out Vector3 B_Scale, out Quaternion B_Rot, out Vector3 B_Trans);

			Vector3 Scale = Lerp(A_Scale, B_Scale, Amt);
			Quaternion Rot = Quaternion.Slerp(A_Rot, B_Rot, Amt);
			Vector3 Trans = Lerp(A_Trans, B_Trans, Amt);

			return CreateMatrix(Trans, Rot, Scale);
		}

		public static Matrix4x4 CreateMatrix(Vector3 Translate, Quaternion Rotate, Vector3 Scale) {
			Matrix4x4 SclMat = Matrix4x4.CreateScale(Scale);
			Matrix4x4 RotMat = Matrix4x4.CreateFromQuaternion(Rotate);
			Matrix4x4 PosMat = Matrix4x4.CreateTranslation(Translate);
			return RotMat * PosMat * SclMat;
		}

		public static Vector3 Clamp(Vector3 Val, Vector3 MinVal, Vector3 MaxVal) {
			return Max(MinVal, Min(Val, MaxVal));
		}

		public static Vector4 Clamp(Vector4 Val, Vector4 MinVal, Vector4 MaxVal) {
			return Max(MinVal, Min(Val, MaxVal));
		}

		public static Vector3 Pow(Vector3 A, Vector3 B) {
			return new Vector3((float)Math.Pow(A.X, B.X), (float)Math.Pow(A.Y, B.Y), (float)Math.Pow(A.Z, B.Z));
		}

		public static byte[] ReadBytes(byte* BytesPtr, int Len, bool Reverse = false) {
			byte[] Bytes = new byte[Len];

			for (int i = 0; i < Bytes.Length; i++)
				Bytes[i] = BytesPtr[i];

			if (Reverse)
				Bytes = Bytes.Reverse().ToArray();

			return Bytes;
		}

		public static uint RotateLeft(uint Val, byte Amt) {
			return (Val << Amt) | (Val >> (32 - Amt));
		}
	}
}
