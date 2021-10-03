using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace App
{
    public static class Finder
    {
        public static List<string> AllFilesIn(string root)
        {
            if (File.Exists(root))
            {
                return  new List<string> {root};
            }

            if (Directory.Exists(root))
            {
                var paths = new List<string>();
                
                try
                {
                    Directory.GetFileSystemEntries(root).ToList()
                        .ForEach(path => paths.AddRange(AllFilesIn(path)));
                }
                catch (Exception)
                {
                    // ignored
                }

                return paths;
            }

            throw new Exception("Cannot find " + root);
        }
    }
}