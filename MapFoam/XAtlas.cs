using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	// A group of connected faces, belonging to a single atlas.
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct XAtlasChart {
		public uint atlasIndex; // Sub-atlas index.
		public uint flags;
		public uint* faceArray;
		public uint faceCount;
		public uint material;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct XAtlasVertex {
		public int atlasIndex; // Sub-atlas index. -1 if the vertex doesn't exist in any atlas.
		public int chartIndex; // -1 if the vertex doesn't exist in any chart.
		public Vector2 UV;
		public uint xref; // Index of input vertex from which this output vertex originated
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct XAtlasMesh {
		public XAtlasChart* chartArray;
		public uint chartCount;
		public uint* indexArray;
		public uint indexCount;
		public XAtlasVertex* vertexArray;
		public uint vertexCount;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct AtlasStruct {
		public uint width; // Atlas width in texels.
		public uint height; // Atlas height in texels.
		public uint atlasCount; // Number of sub-atlases. Equal to 0 unless PackOptions resolution is changed from default (0).
		public uint chartCount; // Total number of charts in all meshes.
		public uint meshCount; // Number of output meshes. Equal to the number of times AddMesh was called.
		public XAtlasMesh* meshes; // The output meshes, corresponding to each AddMesh call.
		public float* utilization; // Normalized atlas texel utilization array. E.g. a value of 0.8 means 20% empty space. atlasCount in length.
		public float texelsPerUnit; // Equal to PackOptions texelsPerUnit if texelsPerUnit > 0, otherwise an estimated value to match PackOptions resolution.
		public FastColor* image;
	}

	// Input mesh declaration.
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct XAtlas_MeshDecl {
		public uint vertexCount;
		public void* vertexPositionData;
		public uint vertexPositionStride;
		public void* vertexNormalData;
		public uint vertexNormalStride;
		public void* vertexUvData;
		public uint vertexUvStride;
		public uint indexCount;
		public void* indexData;
		public int indexOffset;
		public int indexFormat;
		public bool* faceIgnoreData;
		public float epsilon;

		public XAtlas_MeshDecl(Vector3[] Verts) {
			vertexPositionData = null;
			vertexNormalData = null;
			vertexNormalStride = 0;
			vertexUvData = null;
			vertexUvStride = 0;
			indexCount = 0;
			indexData = null;
			indexOffset = 0;
			indexFormat = 0;
			faceIgnoreData = null;
			epsilon = 1.192092896e-07F;

			vertexCount = (uint)Verts.Length;
			vertexPositionStride = sizeof(float) * 3;

			int VertLen = sizeof(float) * 3 * Verts.Length;
			vertexPositionData = Marshal.AllocHGlobal(VertLen).ToPointer();

			fixed (Vector3* VertsPtr = Verts)
				Buffer.MemoryCopy((void*)VertsPtr, vertexPositionData, VertLen, VertLen);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct ChartOptions {
		public float maxChartArea; // Don't grow charts to be larger than this. 0 means no limit.
		public float maxBoundaryLength; // Don't grow charts to have a longer boundary than this. 0 means no limit.

		// Weights determine chart growth. Higher weights mean higher cost for that metric.
		public float proxyFitMetricWeight; // Angle between face and average chart normal.
		public float roundnessMetricWeight;
		public float straightnessMetricWeight;
		public float normalSeamMetricWeight; // If > 1000, normal seams are fully respected.
		public float textureSeamMetricWeight;

		public float maxThreshold; // If total of all metrics * weights > maxThreshold, don't grow chart. Lower values result in more charts.
		public uint maxIterations; // Number of iterations of the chart growing and seeding phases. Higher values result in better charts.

		public static ChartOptions CreateOptions() {
			ChartOptions Opt = new ChartOptions();

			Opt.maxChartArea = 0.0f; // Don't grow charts to be larger than this. 0 means no limit.
			Opt.maxBoundaryLength = 0.0f; // Don't grow charts to have a longer boundary than this. 0 means no limit.

			// Weights determine chart growth. Higher weights mean higher cost for that metric.
			Opt.proxyFitMetricWeight = 2.0f; // Angle between face and average chart normal.
			Opt.roundnessMetricWeight = 0.01f;
			Opt.straightnessMetricWeight = 6.0f;
			Opt.normalSeamMetricWeight = 4.0f; // If > 1000, normal seams are fully respected.
			Opt.textureSeamMetricWeight = 0.5f;

			Opt.maxThreshold = 2.0f; // If total of all metrics * weights > maxThreshold, don't grow chart. Lower values result in more charts.
			Opt.maxIterations = 1; // Number of iterations of the chart growing and seeding phases. Higher values result in better charts.


			return Opt;
		}
	}

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	unsafe delegate void ParameterizeFunc(float* positions, float* texcoords, uint vertexCount, uint* indices, uint indexCount);

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct PackOptions {
		public bool bilinear;
		public bool blockAlign;
		public bool bruteForce;
		public bool createImage;
		public uint maxChartSize;
		public uint padding;
		public float texelsPerUnit;
		public uint resolution;

		public static PackOptions CreatePackOptions() {
			PackOptions Opt = new PackOptions();

			// Leave space around charts for texels that would be sampled by bilinear filtering.
			Opt.bilinear = true;

			// Align charts to 4x4 blocks. Also improves packing speed, since there are fewer possible chart locations to consider.
			Opt.blockAlign = false;

			// Slower, but gives the best result. If false, use random chart placement.
			Opt.bruteForce = true;

			// Create Atlas::image
			Opt.createImage = false;

			// Charts larger than this will be scaled down. 0 means no limit.
			Opt.maxChartSize = 0;

			// Number of pixels to pad charts with.
			Opt.padding = 10;

			// Unit to texel scale. e.g. a 1x1 quad with texelsPerUnit of 32 will take up approximately 32x32 texels in the atlas.
			// If 0, an estimated value will be calculated to approximately match the given resolution.
			// If resolution is also 0, the estimated value will approximately match a 1024x1024 atlas.
			Opt.texelsPerUnit = 0;

			// If 0, generate a single atlas with texelsPerUnit determining the final resolution.
			// If not 0, and texelsPerUnit is not 0, generate one or more atlases with that exact resolution.
			// If not 0, and texelsPerUnit is 0, texelsPerUnit is estimated to approximately match the resolution.
			Opt.resolution = 0;

			return Opt;
		}
	};

	static unsafe class XAtlas {
		const string DllName = "xatlas";
		const CallingConvention CConv = CallingConvention.Cdecl;

		[DllImport(DllName, CallingConvention = CConv)]
		public static extern AtlasStruct* Create();

		[DllImport(DllName, CallingConvention = CConv)]
		public static extern int AddMesh(AtlasStruct* Atlas, ref XAtlas_MeshDecl MeshDecl, uint MeshCountHit = 0);

		[DllImport(DllName, CallingConvention = CConv)]
		public static extern void Generate(AtlasStruct* atlas, ChartOptions chartOptions, ParameterizeFunc paramFunc, PackOptions packOptions);
	}
}
