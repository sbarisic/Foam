using Foam;
using RaylibSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Test {
	unsafe class Program {
		const float Pi = (float)Math.PI;
		static string[] ImgExtensions = new string[] { ".png", ".tga", ".jpg" };

		static Vertex3 ToVert3(FoamVertex3 V) {
			V.UV = new Vector2(V.UV.X, 1.0f - V.UV.Y);

			return new Vertex3(V.Position, V.UV);
		}

		static Texture2D LoadTexture(string FileName) {
			string DirName = Path.GetDirectoryName(FileName);
			string Name = Path.GetFileNameWithoutExtension(FileName);

			foreach (var ImgExt in ImgExtensions) {
				string NewFileName = Path.Combine(DirName, Name + ImgExt);

				if (File.Exists(NewFileName)) {
					FileName = NewFileName;
					break;
				}
			}

			if (!File.Exists(FileName))
				FileName = "data/missing.png";


			Image Img = Raylib.LoadImage(FileName);

			Texture2D Tex = Raylib.LoadTextureFromImage(Img);
			Raylib.GenTextureMipmaps(&Tex);
			Raylib.SetTextureFilter(Tex, TextureFilterMode.FILTER_TRILINEAR);

			return Tex;
		}

		static void SetTexture(Model Mdl, Texture2D Tex) {
			Raylib.SetMaterialTexture(&Mdl.materials[0], MaterialMapType.MAP_ALBEDO, Tex);
		}

		static void SetTexture(Model Mdl, string FileName) {
			SetTexture(Mdl, LoadTexture(FileName));
		}

		static Model FoamMeshToModel(string RootDir, FoamMesh Mesh) {
			Vertex3[] Verts = Mesh.GetFlatVertices().Select(V => ToVert3(V)).ToArray();
			Mesh RaylibMesh = Raylib.GenMeshRaw(Verts);

			Model Mdl = Raylib.LoadModelFromMesh(RaylibMesh);
			//Mdl.transform = Matrix4x4.CreateFromYawPitchRoll(0, Pi / 2, 0);

			if (Mesh.Material.FindTexture(FoamTextureType.Diffuse, out FoamTexture Tex))
				SetTexture(Mdl, Path.Combine(RootDir, Tex.Name));

			return Mdl;
		}

		static Model[] LoadModels(string FileName, out float Scale) {
			string RootDir = Path.GetDirectoryName(FileName);
			Scale = 0;

			try {
				FoamModel FoamModel = FoamModel.FromFile(FileName);
				FoamModel.CalcBounds(out Vector3 Min, out Vector3 Max);
				Scale = Utils.Max(Max - Min);

				return FoamModel.Meshes.Select(M => FoamMeshToModel(RootDir, M)).ToArray();
			} catch (Exception E) {
				Console.WriteLine("{0}", E.Message);
			}

			return null;
		}

		static void Main(string[] args) {
			Raylib.InitWindow(1366, 768, "Foam Test");
			Raylib.SetTargetFPS(60);
			Camera3D Cam3D = new Camera3D(new Vector3(1, 1, 1), Vector3.Zero, Vector3.UnitY);
			Raylib.SetCameraMode(Cam3D, CameraMode.CAMERA_ORBITAL);

			Model[] Models = null;

			while (!Raylib.WindowShouldClose()) {
				if (Raylib.IsFileDropped()) {
					if (Models != null) {
						foreach (var M in Models)
							Raylib.UnloadModel(M);
					}

					Models = LoadModels(Raylib.GetDroppedFiles()[0], out float Scale);
					Cam3D.position = new Vector3(1) * Scale;

					Raylib.ClearDroppedFiles();
				}

				Raylib.BeginDrawing();
				Raylib.ClearBackground(new Color(50, 50, 50));

				if (Models != null) {
					Raylib.UpdateCamera(ref Cam3D);
					Raylib.BeginMode3D(Cam3D);

					for (int i = 0; i < Models.Length; i++)
						Raylib.DrawModel(Models[i], Vector3.Zero, 1, Color.White);

					Raylib.EndMode3D();
				}

				Raylib.DrawFPS(10, 10);
				Raylib.EndDrawing();
			}
		}
	}
}
