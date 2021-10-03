using System.IO;
using Lib;

namespace App
{
    internal static class Program
    {
        private static void Main()
        {
            var outputPath = InputUtils.InputString("Enter path to output folder:");
            var inputPath = InputUtils.InputString("Enter path to folder with classes:");
            var maxDegreeOfParallelism = InputUtils.InputInt("Enter max degree of parallelism");

            if (outputPath == null || inputPath == null)
                return;

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            var testsGenerator = new TestsGenerator(outputPath, maxDegreeOfParallelism == 0 ? 10 : maxDegreeOfParallelism);
            testsGenerator
                .Generate(Finder.AllFilesIn(inputPath))
                .Wait();
        }
    }
}