using Embree;
using Foam;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	static class LightMapping {
		public static TriangleMesh[] MeshesFromFoam(Device Dev, FoamModel Model) {
			TriangleMesh[] Meshes = new TriangleMesh[Model.Meshes.Length];

			for (int i = 0; i < Meshes.Length; i++) {
				List<int> Inds = new List<int>(Model.Meshes[i].Indices.Select(I => (int)I));
				List<IEmbreePoint> Verts = new List<IEmbreePoint>();

				foreach (var Vert in Model.Meshes[i].Vertices)
					Verts.Add(new Point(Vert.Position));

				Meshes[i] = new TriangleMesh(Dev, Inds, Verts);
			}

			return Meshes;
		}

		public static Model LoadModel(Device Dev, FoamModel FoamModel) {
			Model Model = new Model(Dev, Matrix.Identity);

			TriangleMesh[] Meshes = MeshesFromFoam(Dev, FoamModel);
			foreach (var Mesh in Meshes)
				Model.AddMesh(Mesh, new Diffuse(new Vector(0.95f, 0.85f, 0.05f) * 0.318f));

			return Model;
		}

		static Random Rnd = new Random();

		static float RndFloat() {
			return (float)Rnd.NextDouble();
		}

		static float RndFloatNeg() {
			return RndFloat() - 0.5f;
		}

		static Vector3 RandomDir() {
			float z;
			float t;

			lock (Rnd) {
				z = 2.0f * (float)Rnd.NextDouble() - 1.0f;
				t = 2.0f * (float)Rnd.NextDouble() * 3.14f;
			}

			float r = (float)Math.Sqrt(1.0f - z * z);

			return Vector3.Normalize(new Vector3(r * (float)Math.Cos(t), r * (float)Math.Sin(t), z));
		}

		static float SampleHits(Vector3[] SphereKernel, Vector3 Origin, Vector3 WorldNormal, Scene<Model> ModelScene) {
			int Hits = 0;
			Origin = Origin + WorldNormal * Phi;

			for (int i = 0; i < SphereKernel.Length; i++) {
				Vector3 Dir = SphereKernel[i];
				if (Vector3.Dot(WorldNormal, Dir) < 0)
					Dir = -Dir;

				Ray R = new Ray(Origin, Dir);
				if (ModelScene.Occludes(R, 0, 200))
					Hits++;
			}

			return 1.0f - ((float)Hits / SphereKernel.Length);
		}



		static void BounceSunlight(Scene<Model> Scene, ref Vector3 InColor, Vector3 Origin, Vector3 OriginNormal, Vector3[] Dirs, Vector3[] SphereKernel) {
			Origin = Origin + OriginNormal * Phi;

			for (int i = 0; i < Dirs.Length; i++) {
				Ray R = new Ray(Origin, Dirs[i]);
				//RTC.RayPacket1 Packet = Scene.Intersects(R);
				//Intersection<Model> Intersection = Packet.ToIntersection(Scene);

				if (!Scene.Occludes(R))
					InColor += (new Vector3(1.0f, 0.94f, 0.85f) / 3) / Dirs.Length;

				//Model Mdl = Intersection.Instance;
				//Vector3 HitNormal = Mdl.CorrectNormal(new Vector(Intersection.NX, Intersection.NY, Intersection.NZ));
				//Vector3 HitPoint = Origin + OriginNormal * Intersection.Distance;

				//Plane HitPlane = new Plane(HitNormal, Vector3.Distance(Vector3.Zero, HitPoint));
				//Vector3 NewDir = Vector3.TransformNormal(Dir, Matrix4x4.CreateReflection(HitPlane));

			}
		}

		static void BounceLight(Scene<Model> Scene, ref Vector3 InColor, Vector3 Origin, Vector3 OriginNormal, Light Lt) {
			Origin = Origin + OriginNormal * Phi;

			Vector3 ToLight = Lt.Position - Origin;
			Vector3 ToLightDir = Vector3.Normalize(ToLight);
			Ray LightRay = new Ray(Origin, ToLightDir);

			if (!Scene.Occludes(LightRay, 0, ToLight.Length())) {
				float CosLight = Vector.Dot(OriginNormal, ToLightDir);
				float Attenuation = Lt.Intensity * CosLight / Vector.Dot(ToLight, ToLight);
				InColor += Lt.Color * Attenuation;
			}
		}

		const float Phi = 0.0001f;

		public static void Compute(FoamModel Model, MeshAtlasMap[] AtlasMaps, Light[] Lights) {
			//string SphereObj = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), "models", "sphere_3.obj");
			//Vector3[] SphereDirs = ObjLoader.LoadRaw(SphereObj).Select(V => Vector3.Normalize(V)).ToArray();

			Vector3[] SphereKernel = new Vector3[256];

			// Generate random sphere
			for (int i = 0; i < SphereKernel.Length; i++)
				SphereKernel[i] = RandomDir();


			// Generate sun directions
			Vector3 ToSun = Vector3.Normalize(-new Vector3(1, -1, 1));

			//Vector3[] ToSunDirections = new[] { ToSun };
			Vector3[] ToSunDirections = new Vector3[8];
			const float MaxOffsetToSun = 5;
			for (int i = 0; i < ToSunDirections.Length; i++)
				ToSunDirections[i] = Vector3.Normalize(ToSun * 200 + new Vector3(RndFloatNeg() * MaxOffsetToSun, RndFloatNeg() * MaxOffsetToSun, RndFloatNeg() * MaxOffsetToSun));

			Console.Write("Preparing Embree ... ");
			using (Device Dev = new Device()) {
				Scene<Model> ModelScene = new Scene<Model>(Dev, Flags.SCENE, Flags.TRAVERSAL);
				ModelScene.Add(LoadModel(Dev, Model));
				ModelScene.Commit();
				Console.WriteLine("Done");

				int CursorY = Console.CursorTop;

				Parallel.ForEach(AtlasMaps, (AtlasMap) => {
					int MaxIdx = AtlasMap.Width * AtlasMap.Height;
					int LastPercentage = 0;

					int ThreadCursorY = CursorY++;

					for (int Y = 0; Y < AtlasMap.Height; Y++) {
						for (int X = 0; X < AtlasMap.Width; X++) {
							if (AtlasMap.TryGet(X, Y, out Vector3 WorldPos, out Vector3 WorldNormal)) {
								// Percentage calculation
								int Idx = Y * AtlasMap.Width + X;
								int Percentage = (int)((float)Idx / MaxIdx * 100);
								if (Percentage > LastPercentage) {
									LastPercentage = Percentage;

									Console.CursorTop = ThreadCursorY;
									Console.CursorLeft = 0;
									Console.Write("% {0} ", Percentage);
								}

								//WorldPos = WorldPos + WorldNormal * Phi;

								Vector3 Clr = new Vector3(0);
								BounceSunlight(ModelScene, ref Clr, WorldPos, WorldNormal, ToSunDirections, SphereKernel);

								//if (!(Clr.X >= 1 && Clr.Y >= 1 && Clr.Z >= 1))
								Clr += new Vector3(SampleHits(SphereKernel, WorldPos, WorldNormal, ModelScene) / 4);

								for (int i = 0; i < Lights.Length; i++)
									BounceLight(ModelScene, ref Clr, WorldPos, WorldNormal, Lights[i]);

								
								// Gamma
								Clr = Utils.Pow(Clr, new Vector3(1.0f / 2.0f));
								Clr = Utils.Clamp(Clr, Vector3.Zero, Vector3.One);

								AtlasMap.Atlas.SetPixel(X, Y, new FastColor((byte)(Clr.X * 255), (byte)(Clr.Y * 255), (byte)(Clr.Z * 255)));
							}
						}
					}
				});

				Console.CursorLeft = 0;
				Console.CursorTop = CursorY;
				Console.WriteLine("Done!");

				/*Ray R = new Ray(new Point(0, 0, 0), new Vector(0, 0, 1));
				RTC.RayPacket1 Packet = ModelScene.Intersects(R, 0, 100);
				Intersection<Model> Intersect = Packet.ToIntersection(ModelScene);*/


			}
		}
	}

	public class Flags {
		public const SceneFlags SCENE = SceneFlags.Static | SceneFlags.Coherent | SceneFlags.HighQuality | SceneFlags.Robust;
		public const TraversalFlags TRAVERSAL = TraversalFlags.Single | TraversalFlags.Packet4 | TraversalFlags.Packet8;
	}

	/// <summary>
	/// The Model implements the Embree.NET IInstance interface,
	/// and as such manages a model by wrapping it with material
	/// and transform matrix. Advanced renderers might add other
	/// things like texture coordinates and so on.
	/// </summary>
	public class Model : IInstance, IDisposable {
		private readonly Dictionary<IMesh, IMaterial> materials = new Dictionary<IMesh, IMaterial>();
		private Matrix transform, inverseTranspose;
		private readonly Geometry geometry;

		/// <summary>
		/// Gets the wrapped Geometry collection.
		/// </summary>
		public Geometry Geometry { get { return geometry; } }

		/// <summary>
		/// Gets or sets whether this model is enabled.
		/// </summary>
		public Boolean Enabled { get; set; }

		/// <summary>
		/// Gets the material associated with a mesh.
		/// </summary>
		public IMaterial Material(IMesh mesh) {
			return materials[mesh];
		}

		/// <summary>
		/// Gets the transform associated with this model.
		/// </summary>
		public IEmbreeMatrix Transform { get { return transform; } }

		/// <summary>
		/// Creates a new empty model.
		/// </summary>
		public Model(Device device, Matrix transform) {
			Enabled = true;
			this.transform = transform;
			inverseTranspose = Matrix.InverseTranspose(transform);
			geometry = new Geometry(device, Flags.SCENE, Flags.TRAVERSAL);
		}

		/// <summary>
		/// Adds a mesh to this model with a given material.
		/// </summary>
		public void AddMesh(IMesh mesh, IMaterial material) {
			geometry.Add(mesh);
			materials.Add(mesh, material);
		}

		/// <summary>
		/// Corrects an Embree.NET normal, which is unnormalized
		/// and in object space, to a world space normal vector.
		/// </summary>
		public Vector CorrectNormal(Vector normal) {
			return (inverseTranspose * normal).Normalize();
		}

		#region IDisposable

		~Model() {
			Dispose(false);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				geometry.Dispose();
			}
		}

		#endregion
	}

	/// <summary>
	/// The renderer keeps a collection of Embree.NET meshes instanced as Models.
	/// </summary>
	public class Renderer : IDisposable {
		private readonly Dictionary<String, IMesh> meshes = new Dictionary<String, IMesh>();

		private readonly float lightIntensity;
		private readonly Point lightPosition;
		private readonly Camera camera;
		private readonly Scene<Model> scene;


		/// <summary>
		/// Creates a new renderer.
		/// </summary>
		public Renderer(Device device) {
			// Create an Embree.NET scene using our Model type
			scene = new Scene<Model>(device, Flags.SCENE, Flags.TRAVERSAL);

			// Load all required meshes here

			//meshes.Add("buddha", ObjLoader.LoadMesh(device, "Models/buddha.obj"));
			//meshes.Add("lucy", ObjLoader.LoadMesh(scene.Device, "Models/lucy.obj"));
			//meshes.Add("ground", ObjLoader.LoadMesh(scene.Device, "Models/ground.obj"));

			// Create a few Model instances with a given modelworld matrix which we will populate later

			var buddhaModel = new Model(scene.Device, Matrix.Combine(Matrix.Scaling(8),
													   Matrix.Rotation(-(float)Math.PI / 2, 0, 0.5f),
													   Matrix.Translation(new Vector(-2.5f, -1.8f, -4.5f))));

			var lucyModel = new Model(scene.Device, Matrix.Combine(Matrix.Scaling(1.0f / 175),
													 Matrix.Rotation(0, (float)Math.PI / 2 + 2.1f, 0),
													 Matrix.Translation(new Vector(-11, -1.56f, -5))));

			var lucyModel2 = new Model(scene.Device, Matrix.Combine(Matrix.Scaling(1.0f / 600),
													  Matrix.Rotation(0, (float)Math.PI / 2 - 1.8f, 0),
													  Matrix.Translation(new Vector(-2.5f, -3.98f, -8))));

			var groundModel = new Model(scene.Device, Matrix.Combine(Matrix.Scaling(100),
													   Matrix.Translation(new Vector(0, -5, 0))));

			// Now place these meshes into the world with a given material

			buddhaModel.AddMesh(meshes["buddha"], new Phong(new Vector(0.55f, 0.25f, 0.40f), 0.65f, 48));
			lucyModel.AddMesh(meshes["lucy"], new Phong(new Vector(0.35f, 0.65f, 0.15f), 0.85f, 256));
			groundModel.AddMesh(meshes["ground"], new Phong(new Vector(0.25f, 0.25f, 0.95f), 0.45f, 1024));
			lucyModel2.AddMesh(meshes["lucy"], new Diffuse(new Vector(0.95f, 0.85f, 0.05f) * 0.318f)); // instancing example

			// And finally add them to the scene (into the world)

			scene.Add(buddhaModel);
			scene.Add(lucyModel);
			scene.Add(lucyModel2);
			scene.Add(groundModel);

			// Don't forget to commit when we're done messing with the geometry

			scene.Commit();

			// Place a light source somewhere

			lightPosition = new Point(-11.85f, 11, -13);
			lightIntensity = 900;

			// Get a good shot of the world

			camera = new Camera((float)Math.PI / 5, 1,    // unknown aspect ratio for now
								new Point(-2.5f, -0.45f, -12), // good position for the camera
								new Vector(0, 0, 1), 0);  // view direction + no roll (upright)
		}

		/// <summary>
		/// Renders the scene into a pixel buffer.
		/// </summary>
		public void Render(PixelBuffer pixbuf, TraversalFlags mode = TraversalFlags.Single) {
			float dx = 1.0f / pixbuf.Width, dy = 1.0f / pixbuf.Height;
			camera.AspectRatio = (float)pixbuf.Width / pixbuf.Height;

			// Free parallelism, why not! Note a Parallel.For loop
			// over each row is slightly faster but less readable.
			Parallel.ForEach(pixbuf, (pixel) => {
				var color = Vector.Zero;
				float u = pixel.X * dx;
				float v = pixel.Y * dy;

				Ray[] rays = null;
				Intersection<Model>[] hits = null;
				if (mode == TraversalFlags.Single) {
					rays = new[] { camera.Trace(2 * (u - 0.25f * dx) - 1, 2 * (v - 0.25f * dy) - 1) };
					var packet = scene.Intersects(rays[0]);
					hits = new Intersection<Model>[] { packet.ToIntersection<Model>(scene) };
				} else if (mode == TraversalFlags.Packet4) {
					rays = new[]
					{
						camera.Trace(2 * (u - 0.25f * dx) - 1, 2 * (v - 0.25f * dy) - 1),
						camera.Trace(2 * (u + 0.25f * dx) - 1, 2 * (v - 0.25f * dy) - 1),
						camera.Trace(2 * (u - 0.25f * dx) - 1, 2 * (v + 0.25f * dy) - 1),
						camera.Trace(2 * (u + 0.25f * dx) - 1, 2 * (v + 0.25f * dy) - 1)
					};
					// Trace a packet of coherent AA rays
					var packet = scene.Intersects4(rays);
					// Convert the packet to a set of usable ray-geometry intersections
					hits = packet.ToIntersection<Model>(scene);
				} else if (mode == TraversalFlags.Packet8) {
					// Sampling pattern Rotated grid 
					// https://en.wikipedia.org/wiki/Supersampling#Supersampling_patterns
					// ------------
					// | X   X    | 
					// |   X    X |
					// | X    X   |
					// |    X   X |
					// ------------
					//https://www.desmos.com/calculator/l2ynkbsahy
					rays = new[]
					{
						camera.Trace(2 * (u - 0.333f * dx) - 1, 2 * (v - 0.166f * dy) - 1),
						camera.Trace(2 * (u - 0.166f * dx) - 1, 2 * (v - 0.333f * dy) - 1),
						camera.Trace(2 * (u - 0.300f * dx) - 1, 2 * (v + 0.300f * dy) - 1),
						camera.Trace(2 * (u - 0.100f * dx) - 1, 2 * (v + 0.100f * dy) - 1),
						camera.Trace(2 * (u + 0.100f * dx) - 1, 2 * (v - 0.100f * dy) - 1),
						camera.Trace(2 * (u + 0.300f * dx) - 1, 2 * (v - 0.300f * dy) - 1),
						camera.Trace(2 * (u + 0.166f * dx) - 1, 2 * (v + 0.333f * dy) - 1),
						camera.Trace(2 * (u + 0.333f * dx) - 1, 2 * (v + 0.166f * dy) - 1)
					};
					// Trace a packet of coherent AA rays
					var packet = scene.Intersects8(rays);
					// Convert the packet to a set of usable ray-geometry intersections
					hits = packet.ToIntersection<Model>(scene);
				} else {
					throw new Exception("Invalid mode");
				}

				for (int t = 0; t < hits.Length; ++t) {
					if (hits[t].HasHit) {
						color += new Vector(0.1f, 0.1f, 0.1f);

						var ray = rays[t];
						var model = hits[t].Instance;

						// Parse the surface normal returned and then process it manually
						var rawNormal = new Vector(hits[t].NX, hits[t].NY, hits[t].NZ);
						var normal = model.CorrectNormal(rawNormal); // Important!

						// Calculate the new ray towards the light source
						var hitPoint = ray.PointAt(hits[t].Distance);
						var toLight = lightPosition - hitPoint; // from A to B = B - A
						var lightRay = new Ray(hitPoint + normal * Constants.Epsilon, toLight);

						// Is the light source occluded? If so, no point calculating any lighting
						if (!scene.Occludes(lightRay, 0, toLight.Length())) {
							// Compute the Lambertian cosine term (rendering equation)
							float cosLight = Vector.Dot(normal, toLight.Normalize());

							// Calculate the total light attenuation (inverse square law + cosine law)
							var attenuation = lightIntensity * cosLight / Vector.Dot(toLight, toLight);

							color += model.Material(hits[t].Mesh).BRDF(toLight.Normalize(), ray.Direction, normal) * attenuation;
						}
					}
				}
				// Average the per-pixel samples
				pixbuf.SetColor(pixel, color / rays.Length);
			});
		}

		#region IDisposable

		~Renderer() {
			Dispose(false);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				foreach (var model in scene)
					model.Dispose();

				scene.Dispose();
			}
		}

		#endregion
	}
}
