using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace SEMeshGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Please provide file to convert");
                return;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                filePath = Environment.CurrentDirectory + (filePath.StartsWith(Path.PathSeparator) ? filePath : Path.PathSeparator + filePath);
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine("Specified file not found: " + filePath);
            }

            string outputFileName = "outputVertices_" + Path.GetFileNameWithoutExtension(filePath);
            int id = 0;
            string fullOutputPath = Environment.CurrentDirectory + "\\" + outputFileName + id + ".txt";
            while (File.Exists(fullOutputPath))
            {
                id++;
                fullOutputPath = Environment.CurrentDirectory + "\\" + outputFileName + id + ".txt";
            }
            Console.WriteLine("Writing result to: " + fullOutputPath);

            var inputText = File.ReadAllText(filePath);

            Regex vertexCountRegex = new Regex(@"element vertex (\d*)");
            Regex faceCountRegex = new Regex(@"element face (\d*)");
            Regex vertexRegex = new Regex("([^ ]*) ([^ ]*) ([^ ]*)");
            Regex triangleRegex = new Regex(". ([^ ]*) ([^ ]*) ([^ ]*)");
            Regex dataRegex = new Regex(@"end_header\n((.|\\n)*)", RegexOptions.Singleline);

            var vertexCountMatch = vertexCountRegex.Match(inputText);
            var faceCountMatch = faceCountRegex.Match(inputText);

            Vector3[] vertices = new Vector3[int.Parse(vertexCountMatch.Groups[1].Value)];
            int[] triangles = new int[int.Parse(faceCountMatch.Groups[1].Value) * 3];

            string actualMeshData = dataRegex.Match(inputText).Groups[1].Value;
            var fileLines = actualMeshData.Split("\n");

            Console.WriteLine("Actual mesh data:");
            Console.WriteLine(actualMeshData);

            for (int j = 0; j < vertices.Length; j++)
            {
                var vertexMatch = vertexRegex.Match(fileLines[j]);

                vertices[j] = new Vector3(float.Parse(vertexMatch.Groups[1].Value.Replace(".", ",")), float.Parse(vertexMatch.Groups[2].Value.Replace(".", ",")), float.Parse(vertexMatch.Groups[3].Value.Replace(".", ",")));
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int lineIndex = vertices.Length + i / 3;
                var triangleMatch = triangleRegex.Match(fileLines[lineIndex]);
                if (string.IsNullOrEmpty(fileLines[lineIndex]))
                {
                    continue;
                }
                triangles[i] = int.Parse(triangleMatch.Groups[1].Value);
                triangles[i + 1] = int.Parse(triangleMatch.Groups[2].Value);
                triangles[i + 2] = int.Parse(triangleMatch.Groups[3].Value);
            }

            Console.WriteLine("Vertex count: " + vertices.Length);
            Console.WriteLine("Triangle count: " + triangles.Length / 3);

            string output = "public Vector3[] " + Path.GetFileNameWithoutExtension(filePath) + "_vertices = new Vector3[" + vertices.Length + "] {";
            int cnt = 0;
            foreach (var item in vertices)
            {
                output += $"\nnew Vector3({item.X.ToString().Replace(",", ".")}f, {item.Y.ToString().Replace(",", ".")}f, {item.Z.ToString().Replace(",", ".")}f),";
            }
            output += "\n};\n\n";
            cnt = 0;
            output += "public int[] " + Path.GetFileNameWithoutExtension(filePath) + "_triangles = new int[" + triangles.Length + "] {\n";
            foreach (var item in triangles)
            {
                cnt++;
                output += $"{item}, ";
                if (cnt % 3 == 0)
                {
                    output += "\n";
                }
            }
            output += "};";

            File.WriteAllText(fullOutputPath, output);

            Process.Start("notepad.exe", fullOutputPath);
        }
    }
}
