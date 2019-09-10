using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Foam.Loaders {
	public interface ModelLoader {
		bool CanLoad(Stream S, string FileName);
		FoamModel Load(Stream S, string FileName);
	}
}
