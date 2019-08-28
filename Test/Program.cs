using RaylibSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test {
	class Program {
		static void Main(string[] args) {
			Raylib.InitWindow(1366, 768, "Foam Test");
			Raylib.SetTargetFPS(60);

			while (!Raylib.WindowShouldClose()) {
				Raylib.BeginDrawing();
				Raylib.ClearBackground(new Color(50, 50, 50));

				Raylib.DrawFPS(10, 10);
				Raylib.EndDrawing();
			}
		}
	}
}
