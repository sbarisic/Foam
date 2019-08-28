using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

		public static void WriteStructArray<T>(this BinaryWriter Writer, T[] Arr) where T : unmanaged {
			Writer.Write(Arr.Length);
			int LenBytes = Arr.Length * sizeof(T);

			fixed (T* ArrPtr = Arr) {
				byte* BytePtr = (byte*)ArrPtr;

				for (int i = 0; i < LenBytes; i++)
					Writer.Write(BytePtr[i]);
			}
		}

		public static T[] ReadStructArray<T>(this BinaryReader Reader) where T : unmanaged {
			T[] Arr = new T[Reader.ReadInt32()];
			int LenBytes = Arr.Length * sizeof(T);

			fixed (T* ArrPtr = Arr) {
				byte* BytePtr = (byte*)ArrPtr;

				for (int i = 0; i < LenBytes; i++)
					BytePtr[i] = Reader.ReadByte();
			}

			return Arr;
		}

		public static byte[] ToByteArray(byte* Ptr, int Len) {
			byte[] Bytes = new byte[Len];

			for (int i = 0; i < Len; i++)
				Bytes[i] = Ptr[i];

			return Bytes;
		}
	}
}
