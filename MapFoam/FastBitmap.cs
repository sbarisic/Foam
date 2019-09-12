using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct FastColor {
		public byte B;
		public byte G;
		public byte R;
		public byte A;

		public FastColor(byte R, byte G, byte B, byte A) {
			this.R = R;
			this.G = G;
			this.B = B;
			this.A = A;
		}

		public FastColor(byte R, byte G, byte B) : this(R, G, B, 255) {
		}

		public FastColor(byte Val) : this(Val, Val, Val) {
		}
	}

	unsafe class FastBitmap {
		public int Width;
		public int Height;

		Bitmap Internal;
		BitmapData Data;

		int Stride;
		FastColor* Scan0;

		public FastBitmap(int Width, int Height) {
			Internal = new Bitmap(Width, Height);
			LockData();

			for (int Y = 0; Y < Height; Y++)
				for (int X = 0; X < Width; X++)
					SetPixel(X, Y, /*new FastColor(255, 0, 0)*/ new FastColor(0, 0, 0, 0));
		}

		public void LockData() {
			Width = Internal.Width;
			Height = Internal.Height;

			Data = Internal.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			Stride = Data.Stride / sizeof(FastColor);
			Scan0 = (FastColor*)Data.Scan0.ToPointer();
		}

		public void UnlockData() {
			Internal.UnlockBits(Data);
			Data = null;
			Scan0 = null;
			Stride = 0;
		}

		public void SetPixel(int X, int Y, FastColor Clr) {
			Scan0[Y * Stride + X] = Clr;
		}

		public FastColor GetPixel(int X, int Y) {
			return Scan0[Y * Stride + X];
		}

		public void Resize(float Scale) {
			UnlockData();

			Bitmap NewInternal = new Bitmap((int)(Width * Scale), (int)(Height * Scale));
			using (Internal) {
				using (Graphics Gfx = Graphics.FromImage(NewInternal)) {
					Gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
					Gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

					Gfx.DrawImage(Internal, 0, 0, NewInternal.Width, NewInternal.Height);
				}
			}

			Internal = NewInternal;
			LockData();
		}

		public void Save(string FileName) {
			UnlockData();
			Internal.Save(FileName);
			LockData();
		}
	}
}
