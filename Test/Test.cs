﻿using Foam;
using RaylibSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
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

			if (!File.Exists(FileName)) {
				Console.WriteLine("Could not find " + FileName);
				FileName = "data/missing.png";
			}


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
			Mdl.transform = Matrix4x4.CreateFromYawPitchRoll(0, Pi / 2, 0);

			Mesh.Userdata = Mdl;

			if (Mesh.Material.FindTexture(FoamTextureType.Diffuse, out FoamTexture Tex))
				SetTexture(Mdl, Path.Combine(RootDir, Tex.Name));

			return Mdl;
		}

		static Model[] LoadModels(string FileName, out float Scale, out FoamModel FoamModel) {
			string RootDir = Path.GetDirectoryName(FileName);
			Scale = 0;
			FoamModel = null;

			try {
				FoamModel = FoamModel.FromFile(FileName);
				FoamModel.CalcBounds(out Vector3 Min, out Vector3 Max);
				Scale = Utils.Max(Max - Min);

				return FoamModel.Meshes.Select(M => FoamMeshToModel(RootDir, M)).ToArray();
			} catch (Exception E) {
				Console.WriteLine("{0}", E.Message);
			}

			return null;
		}

		static int FrameIndex = 0;
		static Stopwatch AnimStopwatch = null;

		static void DrawBones(FoamModel Mdl) {
			if (Mdl.Animations != null) {
				FoamAnimation Anim = Mdl.Animations[0];
				int Frames = Anim.Frames.Length;
				float SecondsPerFrame = (Anim.DurationInTicks / Anim.TicksPerSecond) / Frames;

				if (AnimStopwatch == null)
					AnimStopwatch = Stopwatch.StartNew();

				if ((AnimStopwatch.ElapsedMilliseconds / 1000.0f) >= SecondsPerFrame) {
					FrameIndex++;

					if (FrameIndex >= Anim.Frames.Length)
						FrameIndex = 0;

					UpdateModel(Mdl, FrameIndex);

					AnimStopwatch.Restart();
				}
			}

			if (Mdl.Bones == null)
				return;

			if (WorldTexts == null || WorldTexts.Length != Mdl.Bones.Length) {
				WorldTexts = new KeyValuePair<Vector2, string>[Mdl.Bones.Length];
				for (int i = 0; i < WorldTexts.Length; i++)
					WorldTexts[i] = new KeyValuePair<Vector2, string>(new Vector2(0, 0), "null");
			}

			Matrix4x4 ParentRotMat = Matrix4x4.CreateFromYawPitchRoll(0, -Pi / 2, 0);

			for (int i = 0; i < Mdl.Bones.Length; i++) {
				FoamBone Bone = Mdl.Bones[i];


				if (Mdl.Animations != null) {
					// Actual bones
					Matrix4x4 ParentWorld = Matrix4x4.Identity;
					if (Bone.ParentBoneIndex != -1)
						ParentWorld = Mdl.CalcWorldTransform(0, FrameIndex, Bone.ParentBoneIndex) * ParentRotMat;
					Matrix4x4.Decompose(ParentWorld, out Vector3 ParentScale, out Quaternion ParentRot, out Vector3 ParentPos);

					Matrix4x4 World = Mdl.CalcWorldTransform(0, FrameIndex, i) * ParentRotMat;
					Matrix4x4.Decompose(World, out Vector3 Scale, out Quaternion Rot, out Vector3 Pos);

					Raylib.DrawLine3D(ParentPos, Pos, Color.Red);

					// Text
					Vector3 Center = (Pos + ParentPos) / 2;
					WorldTexts[i] = new KeyValuePair<Vector2, string>(Raylib.GetWorldToScreen(Center, Cam3D), Bone.Name);
				}

				/*// Bind pose bones
				Matrix4x4 BindParentWorld = Matrix4x4.Identity;
				if (Bone.ParentBoneIndex != -1)
					BindParentWorld = Mdl.Bones[Bone.ParentBoneIndex].BindMatrix;
				Matrix4x4.Decompose(BindParentWorld, out Vector3 BindParentScale, out Quaternion BindParentRot, out Vector3 BindParentPos);

				Matrix4x4 BindWorld = Mdl.Bones[i].BindMatrix;
				Matrix4x4.Decompose(BindWorld, out Vector3 BindScale, out Quaternion BindRot, out Vector3 BindPos);

				Raylib.DrawLine3D(BindParentPos, BindPos, Color.Green);
				*/
			}
		}





		static void UpdateModel(FoamModel Model, int FrameIndex) {
			if (Model.Animations == null)
				return;

			foreach (var Msh in Model.Meshes) {
				List<Vertex3> Verts = new List<Vertex3>();
				if (Msh.BoneInformation == null)
					continue;

				foreach (var Index in Msh.Indices) {
					FoamVertex3 Vert = Msh.Vertices[Index];
					FoamBoneInfo Info = Msh.BoneInformation[Index];
					Vector3 Pos = Vector3.Zero;

					Matrix4x4 WorldTrans = Model.CalcWorldTransform(0, FrameIndex, Info.Bone1);
					Matrix4x4.Decompose(WorldTrans, out Vector3 _S, out Quaternion Rot, out Vector3 Trans);

					//Pos = Vert.Position;
					Pos = Vector3.Transform(Vert.Position, WorldTrans);


					Verts.Add(new Vertex3(Pos, new Vector2(Vert.UV.X, 1 - Vert.UV.Y)));
				}

				Mesh* RayMesh = ((Model)Msh.Userdata).meshes;
				Raylib.UnloadMesh(*RayMesh);
				*RayMesh = Raylib.GenMeshRaw(Verts.ToArray());
			}
		}

		static KeyValuePair<Vector2, string>[] WorldTexts = null;
		static Camera3D Cam3D;

		static void Main(string[] args) {
			Raylib.InitWindow(1366, 768, "Foam Test");
			Raylib.SetTargetFPS(60);

			Cam3D = new Camera3D(new Vector3(1, 1, 1), Vector3.Zero, Vector3.UnitY);
			Raylib.SetCameraMode(Cam3D, CameraMode.CAMERA_ORBITAL);

			Model[] Models = null;
			FoamModel FoamModel = null;


			while (!Raylib.WindowShouldClose()) {
				if (Raylib.IsFileDropped()) {
					Model[] NewModels = LoadModels(Raylib.GetDroppedFiles()[0], out float Scale, out FoamModel NewFoamModel);

					if (NewModels.Length == 0 && NewFoamModel.Animations != null && FoamModel != null) {
						foreach (var NewAnim in NewFoamModel.Animations)
							Utils.Append(ref FoamModel.Animations, NewAnim);
					} else {
						if (Models != null) {
							foreach (var M in Models)
								Raylib.UnloadModel(M);
						}

						Models = NewModels;
						FoamModel = NewFoamModel;

						Cam3D.position = new Vector3(0.5f, 0.25f, 0.5f) * Scale;
						Cam3D.target = new Vector3(0, 0.25f, 0) * Scale;
					}

					Raylib.ClearDroppedFiles();
				}

				/*if (Models != null)
					UpdateModel(FoamModel, FrameIndex);*/

				Raylib.BeginDrawing();
				Raylib.ClearBackground(new Color(50, 50, 50));

				if (Models != null) {
					Raylib.UpdateCamera(ref Cam3D);
					Raylib.BeginMode3D(Cam3D);


					for (int i = 0; i < Models.Length; i++)
						Raylib.DrawModelWires(Models[i], Vector3.Zero, 1, Color.White);

					if (FoamModel != null)
						DrawBones(FoamModel);

					Raylib.EndMode3D();

					if (WorldTexts != null)
						foreach (var KV in WorldTexts)
							Raylib.DrawText(KV.Value, (int)KV.Key.X, (int)KV.Key.Y, 12, Color.White);
				}

				Raylib.DrawFPS(10, 10);
				Raylib.EndDrawing();
			}
		}
	}
}
