using System;
using System.IO;
using System.Reflection;

namespace AspNetLoggingBuildpack
{
    public class AspNetLoggingBuildpack : SupplyBuildpack
    {
        protected override void Apply(string buildPath, string cachePath, string depsPath, int index)
        {
            var currentBuildpackDir = Path.GetDirectoryName(typeof(AspNetLoggingBuildpack).Assembly.Location);
            CopyDirectory(Path.Combine(currentBuildpackDir, "..", "lib"), Path.Combine(depsPath, index.ToString()));
        }
        
        void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            void CopyAll(DirectoryInfo source, DirectoryInfo target)
            {
                Directory.CreateDirectory(target.FullName);

                // Copy each file into the new directory.
                foreach (var fi in source.GetFiles())
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }

                // Copy each subdirectory using recursion.
                foreach (var diSourceSubDir in source.GetDirectories())
                {
                    var nextTargetSubDir =
                        target.CreateSubdirectory(diSourceSubDir.Name);
                    CopyAll(diSourceSubDir, nextTargetSubDir);
                }
            }
            var diSource = new DirectoryInfo(sourceDirectory);
            var diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }
    }
}
