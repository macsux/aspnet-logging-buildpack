using System;
using System.Web;
using HarmonyLib;

namespace AspNetLoggingBuildpackModule
{
    public class AspNetLoggingBuildpackHttpModule: IHttpModule
    {
        public void Init(HttpApplication app)
        {
            try
            {
                var patchId = GetType().FullName;
                var harmony = new Harmony(patchId);
                if (!Harmony.HasAnyPatches(patchId))
                {
                    harmony.PatchAll();
                    Console.WriteLine($"Module {GetType().FullName} initialized");
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Module {GetType().FullName} failed to initialized");
                Console.Error.WriteLine(e);
            }

            app.Error += OnError;
        }

        private void OnError(object sender, EventArgs e)
        {
            var ctx = HttpContext.Current;
            var exception = ctx.Server.GetLastError();
            Console.Error.WriteLine(exception);
        }
       
        public void Dispose()
        {
        }
    }
}