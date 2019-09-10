using Foam.Loaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Foam {
	public static class FoamConverter {
		static List<ModelLoader> Loaders;

		static FoamConverter() {
			Loaders = new List<ModelLoader>();
			Loaders.Add(new DPM());
			Loaders.Add(new IQM());
		}

		public static FoamModel Load(string FileName) {
			using (FileStream FS = File.OpenRead(FileName))
				return Load(FS, FileName);
		}

		public static FoamModel Load(Stream S, string FileName = null) {
			ModelLoader L = FindLoader(S, FileName);

			if (L == null)
				throw new Exception("Can not load model file " + (FileName ?? "NULL"));

			return L.Load(S, FileName);
		}

		static ModelLoader FindLoader(Stream S, string FileName = null) {
			foreach (var L in Loaders) {
				long CurPos = S.Position;

				if (L.CanLoad(S, FileName)) {
					S.Position = CurPos;
					return L;
				}

				S.Position = CurPos;
			}

			return null;
		}
	}
}
