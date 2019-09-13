using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MapFoam {
	class Light {
		public Vector3 Position;
		public Vector3 Color;
		public float Intensity;

		public Light(Vector3 Position, Vector3 Color, float Intensity) {
			this.Position = Position;
			this.Color = Color;
			this.Intensity = Intensity;
		}
	}
}
