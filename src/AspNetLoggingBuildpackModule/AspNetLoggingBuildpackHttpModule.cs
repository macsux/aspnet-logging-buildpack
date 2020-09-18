using System.Web;
using HarmonyLib;

namespace AspNetLoggingBuildpackModule
{
    public class AspNetLoggingBuildpackHttpModule: IHttpModule
    {
        public void Init(HttpApplication context)
        {
            var harmony = new Harmony("AspNetLoggingBuildpack");
            harmony.PatchAll();
        }
       
        public void Dispose()
        {
        }
    }
}