using System;
using System.Linq;

namespace Lifecycle.Supply
{
    class Program
    {
        static int Main(string[] args)
        {
            var argsWithCommand = new[] {"Finalize"}.Concat(args).ToArray();
            return AspNetLoggingBuildpack.Program.Main(argsWithCommand);
        }
    }
}