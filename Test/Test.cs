using Foam;
using RaylibSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Test {
	unsafe class Program {
		static Vertex3 ToVert3(FoamVertex3 V) {
			V.UV = new Vector2(V.UV.X, 1.0f - V.UV.Y);

			return new Vertex3(V.Position, V.UV);
		}

		static Texture2D LoadTexture(string FileName) {
			Image Img = Raylib.LoadImage(FileName);

			Texture2D Tex = Raylib.LoadTextureFromImage(Img);
			Raylib.GenTextureMipmaps(&Tex);
			Raylib.SetTextureFilter(Tex, TextureFilterMode.FILTER_TRILINEAR);

			return Tex;
		}

		static void SetTexture(Model Mdl, Texture2D Tex) {
			Raylib.SetMaterialTexture(&Mdl.materials[0], MaterialMapType.MAP_ALBEDO, Tex);
		}

		static Model FoamMeshToModel(FoamMesh Mesh) {
			Vertex3[] Verts = Mesh.GetFlatVertices().Select(V => ToVert3(V)).ToArray();
			Mesh RaylibMesh = Raylib.GenMeshRaw(Verts);

			Model Mdl = Raylib.LoadModelFromMesh(RaylibMesh);

			switch (Mesh.MaterialName) {
				case "Control_Module":
					SetTexture(Mdl, LoadTexture("models/obj/walker/Control_Module_color.jpg"));
					break;

				case "Neck_Mech":
					SetTexture(Mdl, LoadTexture("models/obj/walker/Hydraulic_col.jpg"));
					break;

				case "Walker":
					SetTexture(Mdl, LoadTexture("models/obj/walker/walker_color.jpg"));
					break;

				case "Walker_Glas":
					SetTexture(Mdl, LoadTexture("models/obj/walker/walker_color.jpg"));
					break;

				case "":
					SetTexture(Mdl, LoadTexture("models/md3/watercan/water_can.tga"));
					break;
			}

			return Mdl;
		}


		static void Main(string[] args) {
			Raylib.InitWindow(1366, 768, "Foam Test");
			Raylib.SetTargetFPS(60);
			Camera3D Cam3D = new Camera3D(new Vector3(1, 1, 1), Vector3.Zero, Vector3.UnitY);
			Raylib.SetCameraMode(Cam3D, CameraMode.CAMERA_ORBITAL);

			FoamHeader FoamModel = FoamHeader.FromFile("watercan.foam");
			Model[] Models = FoamModel.Meshes.Select(M => FoamMeshToModel(M)).ToArray();

			while (!Raylib.WindowShouldClose()) {
				Raylib.BeginDrawing();
				Raylib.ClearBackground(new Color(50, 50, 50));

				Raylib.UpdateCamera(ref Cam3D);
				Raylib.BeginMode3D(Cam3D);

				for (int i = 0; i < Models.Length; i++)
					Raylib.DrawModel(Models[i], Vector3.Zero, 1, Color.White);

				Raylib.EndMode3D();

				Raylib.DrawFPS(10, 10);
				Raylib.EndDrawing();
			}
		}
	}
}
