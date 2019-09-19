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
		[StructLayout(LayoutKind.Explicit, Pack = 1)]
		struct FastColorUnion {
			[FieldOffset(0)]
			public byte R;

			[FieldOffset(1)]
			public byte G;

			[FieldOffset(2)]
			public byte B;

			[FieldOffset(3)]
			public byte A;

			[FieldOffset(0)]
			public int Val;
		}

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

		public int ToInt() {
			FastColorUnion Union = new FastColorUnion();
			Union.R = R;
			Union.G = G;
			Union.B = B;
			Union.A = (byte)(255 - A);
			return Union.Val;
		}

		public static bool IsAlpha(FastColor C) {
			return C.R == 0 && C.G == 0 && C.B == 0 && C.A == 0;
		}

		public static FastColor FromInt(int Val) {
			FastColorUnion Union = new FastColorUnion();
			Union.Val = Val;
			return new FastColor(Union.R, Union.G, Union.B, (byte)(255 - Union.A));
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
					SetPixel(X, Y, new FastColor(0, 0, 0, 0));
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

		public void SetPixelChecked(int X, int Y, FastColor Clr) {
			if (X < 0 || X >= Width || Y < 0 || Y >= Height)
				return;

			SetPixel(X, Y, Clr);
		}

		public FastColor GetPixel(int X, int Y) {
			return Scan0[Y * Stride + X];
		}

		public FastColor GetPixelChecked(int X, int Y) {
			if (X < 0 || X >= Width || Y < 0 || Y >= Height)
				return new FastColor(0, 0, 0, 0);

			return GetPixel(X, Y);
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

		public void UseGraphics(Action<Graphics> Act) {
			UnlockData();

			using (Graphics Gfx = Graphics.FromImage(Internal)) {
				Act(Gfx);
			}

			LockData();
		}

		public void FlipY() {
			UnlockData();
			Internal.RotateFlip(RotateFlipType.RotateNoneFlipY);
			LockData();
		}

		public void Save(string FileName) {
			UnlockData();
			Internal.Save(FileName);
			LockData();
		}

		public Image GetImage() {
			UnlockData();
			return Internal;
		}

		public FastBitmap Extend() {
			FastBitmap NewBitmap = new FastBitmap(Width, Height);

			for (int Y = 0; Y < Height; Y++)
				for (int X = 0; X < Width; X++) {
					FastColor Cur;

					if (FastColor.IsAlpha(Cur = GetPixel(X, Y))) {
						FastColor CopyClr;

						// Top row
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X - 1, Y + 1))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X, Y + 1))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X + 1, Y + 1))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}

						// Cur row
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X - 1, Y))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X + 1, Y))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}

						// Bottom row
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X - 1, Y - 1))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X, Y - 1))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}
						if (!FastColor.IsAlpha(CopyClr = GetPixelChecked(X + 1, Y - 1))) {
							NewBitmap.SetPixel(X, Y, CopyClr);
							continue;
						}
					} else
						NewBitmap.SetPixel(X, Y, Cur);
				}

			return NewBitmap;
		}
	}
}
