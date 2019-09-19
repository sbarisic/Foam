using Foam;
using RaylibSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Color = RaylibSharp.Color;
using Image = RaylibSharp.Image;
using NetImage = System.Drawing.Image;

namespace Test {
	unsafe class Program {
		const float Pi = (float)Math.PI;
		static string[] ImgExtensions = new string[] { ".png", ".tga", ".jpg" };

		static Vertex3 ToVert3(FoamVertex3 V) {
			return new Vertex3(V.Position, new Vector2(V.UV2.X, 1.0f - V.UV2.Y));
		}

		static Texture2D LoadTexture(Image Img) {
			Texture2D Tex = Raylib.LoadTextureFromImage(Img);

			//Raylib.GenTextureMipmaps(&Tex);
			//Raylib.SetTextureFilter(Tex, TextureFilterMode.FILTER_ANISOTROPIC_16X);
			//Raylib.SetTextureWrap(Tex, TextureWrapMode.WRAP_CLAMP);

			Raylib.SetTextureFilter(Tex, TextureFilterMode.FILTER_POINT);
			Raylib.SetTextureWrap(Tex, TextureWrapMode.WRAP_CLAMP);

			return Tex;
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
			return LoadTexture(Img);
		}

		static Texture2D LoadTexture(FoamExtension Ext) {
			using (MemoryStream MS = new MemoryStream(Ext.Data)) {
				MS.Seek(0, SeekOrigin.Begin);

				using (Bitmap Bmp = new Bitmap(NetImage.FromStream(MS))) {
					BitmapData Data = Bmp.LockBits(new System.Drawing.Rectangle(0, 0, Bmp.Width, Bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
					uint* OrigColors = (uint*)Data.Scan0;

					int Len = Bmp.Width * Bmp.Height;
					uint* Colors = (uint*)Marshal.AllocHGlobal(Len * sizeof(uint));

					for (int i = 0; i < Len; i++) {
						uint Orig = OrigColors[i];

						byte R = (byte)((Orig >> 16) & 255);
						byte G = (byte)((Orig >> 8) & 255);
						byte B = (byte)((Orig >> 0) & 255);
						byte A = (byte)((Orig >> 24) & 255);

						Colors[i] = (uint)((R << 0) | (G << 8) | (B << 16) | (A << 24));
					}

					Image Img = Raylib.LoadImagePro(new IntPtr(Colors), Bmp.Width, Bmp.Height, (int)RaylibSharp.PixelFormat.UNCOMPRESSED_R8G8B8A8);
					Marshal.FreeHGlobal(new IntPtr(Colors));
					
					Bmp.UnlockBits(Data);
					return LoadTexture(Img);
				}
			}
		}

		static void SetTexture(Model Mdl, Texture2D Tex) {
			Raylib.SetMaterialTexture(&Mdl.materials[0], MaterialMapType.MAP_ALBEDO, Tex);
		}

		static void SetTexture(Model Mdl, string FileName) {
			SetTexture(Mdl, LoadTexture(FileName));
		}

		static Model FoamMeshToModel(string RootDir, FoamMesh Mesh, FoamModel FoamModel) {
			Vertex3[] Verts = Mesh.GetFlatVertices().Select(V => ToVert3(V)).ToArray();
			Mesh RaylibMesh = Raylib.GenMeshRaw(Verts);

			Model Mdl = Raylib.LoadModelFromMesh(RaylibMesh);
			// Mdl.transform = Matrix4x4.CreateFromYawPitchRoll(0, Pi / 2, 0);

			Mesh.Userdata = Mdl;

			foreach (var Ext in FoamModel.Extensions) {
				if (Ext.Name.Contains("lightmap")) {
					Texture2D LightmapTex = LoadTexture(Ext);

					for (int i = 0; i < FoamModel.Materials.Length; i++) {
						if (FoamModel.Materials[i].FindTexture(FoamTextureType.LightMap, out FoamTexture LightTex))
							SetTexture(Mdl, LightmapTex);
					}
					break;
				}
			}

			if (FoamModel.Materials[Mesh.MaterialIndex].FindTexture(FoamTextureType.Diffuse, out FoamTexture Tex))
				SetTexture(Mdl, Path.Combine(RootDir, Tex.Name));

			//Mdl.transform = Matrix4x4.CreateFromYawPitchRoll(0, Pi / 2, 0);
			Mdl.transform = Matrix4x4.CreateScale(0.01f);
			return Mdl;
		}

		static Model[] LoadModels(string FileName, out float Scale, out FoamModel FoamModel) {
			string RootDir = Path.GetDirectoryName(FileName);
			Scale = 0;
			FoamModel = null;

			string Ext = Path.GetExtension(FileName).ToLower();
			if (!(Ext == ".foam" || Ext == ".mapfoam")) {
				FoamModel = FoamConverter.Load(FileName);
				// FoamModel.SaveToFile(Path.Combine(RootDir, Path.GetFileNameWithoutExtension(FileName) + ".foam"));
			}

			//try {
			if (FoamModel == null)
				FoamModel = FoamModel.FromFile(FileName);

			FoamModel.CalcBounds(out Vector3 Min, out Vector3 Max);
			Scale = Utils.Max(Max - Min);

			List<Model> LoadedModels = new List<Model>();
			foreach (var M in FoamModel.Meshes)
				LoadedModels.Add(FoamMeshToModel(RootDir, M, FoamModel));

			return LoadedModels.ToArray();
			/*} catch (Exception E) {
				Console.WriteLine("{0}", E.Message);
			}*/

			return null;
		}

		static int FrameIndex = 0;
		static Stopwatch AnimStopwatch = null;

		static void DrawBones(FoamModel Mdl) {
			if (Mdl.Bones == null)
				return;

			if (WorldTexts == null || WorldTexts.Length != Mdl.Bones.Length) {
				WorldTexts = new KeyValuePair<Vector2, string>[Mdl.Bones.Length];
				for (int i = 0; i < WorldTexts.Length; i++)
					WorldTexts[i] = new KeyValuePair<Vector2, string>(new Vector2(0, 0), "null");
			}

			//Matrix4x4 ParentRotMat = Matrix4x4.CreateFromYawPitchRoll(0, -Pi / 2, 0);

			for (int i = 0; i < Mdl.Bones.Length; i++) {
				FoamBone Bone = Mdl.Bones[i];


				//*
				if (Mdl.Animations != null) {
					// Actual bones
					Matrix4x4 ParentWorld = Matrix4x4.Identity;
					if (Bone.ParentBoneIndex != -1)
						ParentWorld = Mdl.CalcWorldTransform(0, FrameIndex, Bone.ParentBoneIndex);
					Matrix4x4.Decompose(ParentWorld, out Vector3 ParentScale, out Quaternion ParentRot, out Vector3 ParentPos);

					Matrix4x4 World = Mdl.CalcWorldTransform(0, FrameIndex, i);
					Matrix4x4.Decompose(World, out Vector3 Scale, out Quaternion Rot, out Vector3 Pos);

					DrawLine3D(ParentPos, Pos, Color.Red);

					// Text
					Vector3 Center = (Pos + ParentPos) / 2;
					WorldTexts[i] = new KeyValuePair<Vector2, string>(Raylib.GetWorldToScreen(RotateVec3(Center), Cam3D), Bone.Name);
				}
				//*/


				// Bind pose bones
				Matrix4x4 BindParentWorld = Matrix4x4.Identity;
				if (Bone.ParentBoneIndex != -1)
					BindParentWorld = Mdl.CalcBindTransform(Bone.ParentBoneIndex);
				Matrix4x4.Decompose(BindParentWorld, out Vector3 BindParentScale, out Quaternion BindParentRot, out Vector3 BindParentPos);

				Matrix4x4 BindWorld = Mdl.CalcBindTransform(i);
				Matrix4x4.Decompose(BindWorld, out Vector3 BindScale, out Quaternion BindRot, out Vector3 BindPos);

				DrawLine3D(BindParentPos, BindPos, Color.Green);
				//*/
			}
		}

		static Vector3 RotateVec3(Vector3 V) {
			return Vector3.Transform(V, Matrix4x4.CreateFromYawPitchRoll(0, -Pi / 2, 0));
		}

		static void DrawLine3D(Vector3 A, Vector3 B, Color Clr) {
			Raylib.DrawLine3D(RotateVec3(A), RotateVec3(B), Clr);
		}


		static void UpdateModel(FoamModel Model, int FrameIndex) {
			if (Model.Animations == null)
				return;

			//Matrix4x4 ParentRotMat = Matrix4x4.CreateFromYawPitchRoll(0, -Pi / 2, 0);

			foreach (var Msh in Model.Meshes) {
				List<Vertex3> Verts = new List<Vertex3>();
				if (Msh.BoneInformation == null)
					continue;

				foreach (var Index in Msh.Indices) {
					FoamVertex3 Vert = Msh.Vertices[Index];
					FoamBoneInfo Info = Msh.BoneInformation[Index];
					FoamBone Bone1 = Model.Bones[Info.Bone1];

					// TODO: Weights
					Matrix4x4 BindWorld = Bone1.BindMatrix;
					Matrix4x4 WorldTrans = Model.CalcWorldTransform(0, FrameIndex, Info.Bone1);
					Vector3 Pos = Vector3.Transform(Vert.Position, BindWorld * WorldTrans);

					// TODO: Flip?
					Verts.Add(new Vertex3(Pos, Vert.UV));
				}

				Mesh* RayMesh = ((Model)Msh.Userdata).meshes;
				Raylib.UnloadMesh(*RayMesh);
				*RayMesh = Raylib.GenMeshRaw(Verts.ToArray());
			}
		}

		static void UpdateModelAnimation(FoamModel Mdl) {
			if (Mdl.Animations != null) {
				FoamAnimation Anim = Mdl.Animations[0];
				int Frames = Anim.Frames.Length;

				float TicksPerSecond = Anim.TicksPerSecond;
				if (TicksPerSecond == 0)
					TicksPerSecond = 21;

				float SecondsPerFrame = (Anim.DurationInTicks / TicksPerSecond) / Frames;

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
		}

		static KeyValuePair<Vector2, string>[] WorldTexts = null;
		static Camera3D Cam3D;

		static void Main(string[] args) {
			Raylib.InitWindow(1366, 768, "Foam Test");
			Raylib.SetTargetFPS(60);

			//args = new string[] { "C:/Projekti/Foam/bin/mapfoam/sample/test.mapfoam" };
			//args = new[] { "C:/Projekti/Ray/build/bin/sample/light_test.mapfoam" };

			Cam3D = new Camera3D(new Vector3(1, 1, 1), Vector3.Zero, Vector3.UnitY);
			Raylib.SetCameraMode(Cam3D, CameraMode.CAMERA_FREE);

			Model[] Models = null;
			FoamModel FoamModel = null;

			bool DrawText = true;
			bool DrawWireframe = false;
			bool DrawSkeleton = true;
			bool UpdateAnimation = true;

			while (!Raylib.WindowShouldClose()) {
				if (Raylib.IsKeyPressed(KeyboardKey.KEY_F1))
					DrawText = !DrawText;

				if (Raylib.IsKeyPressed(KeyboardKey.KEY_F2))
					DrawWireframe = !DrawWireframe;

				if (Raylib.IsKeyPressed(KeyboardKey.KEY_F3))
					DrawSkeleton = !DrawSkeleton;

				if (Raylib.IsKeyPressed(KeyboardKey.KEY_F4))
					UpdateAnimation = !UpdateAnimation;

				if (Raylib.IsFileDropped() || (args != null && args.Length > 0)) {
					string DroppedFile = null;

					if (args != null && args.Length > 0) {
						DroppedFile = args[0];
						args = null;
					} else
						DroppedFile = Raylib.GetDroppedFiles()[0];

					Model[] NewModels = LoadModels(DroppedFile, out float Scale, out FoamModel NewFoamModel);

					if (NewModels != null && NewFoamModel != null) {
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

							//Cam3D.position = new Vector3(0.5f, 0.25f, 0.5f) * Scale;
							//Cam3D.target = new Vector3(0, 0.25f, 0) * Scale;
						}
					}

					Raylib.ClearDroppedFiles();
				}

				/*if (Models != null)
					UpdateModel(FoamModel, FrameIndex);*/

				Raylib.BeginDrawing();
				Raylib.ClearBackground(new Color(50, 50, 50));
				//Raylib.ClearBackground(new Color(0, 0, 0));

				if (Models != null) {
					Raylib.UpdateCamera(ref Cam3D);
					Raylib.BeginMode3D(Cam3D);


					for (int i = 0; i < Models.Length; i++) {
						if (DrawWireframe)
							Raylib.DrawModelWires(Models[i], Vector3.Zero, 1, Color.White);
						else
							Raylib.DrawModel(Models[i], Vector3.Zero, 1, Color.White);
					}

					if (UpdateAnimation)
						UpdateModelAnimation(FoamModel);

					if (FoamModel != null && DrawSkeleton)
						DrawBones(FoamModel);

					Raylib.EndMode3D();

					if (DrawText) {
						if (WorldTexts != null)
							foreach (var KV in WorldTexts)
								Raylib.DrawText(KV.Value, (int)KV.Key.X, (int)KV.Key.Y, 10, Color.White);
					}
				}

				DrawTextLine("F1 - Toggle bone names", 0);
				DrawTextLine("F2 - Toggle wireframe", 1);
				DrawTextLine("F3 - Toggle skeleton", 2);
				DrawTextLine("F4 - Toggle animations", 3);

				Raylib.DrawFPS(5, 5);
				Raylib.EndDrawing();
			}
		}

		static void DrawTextLine(string Txt, int Idx) {
			const int FontSize = 10;
			const int YOffset = 30;
			Raylib.DrawText(Txt, 5, (int)(YOffset + FontSize * 1.1f * Idx), FontSize, Color.White);
		}
	}
}
