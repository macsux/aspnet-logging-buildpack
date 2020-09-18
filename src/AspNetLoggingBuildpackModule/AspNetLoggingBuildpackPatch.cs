namespace AspNetLoggingBuildpackModule
{
    // [HarmonyPatch(typeof(DateTime), nameof(ToString), new Type[0])]
    public class MyPatch
    {
        static void Postfix(ref string __result)
        {
            __result = "Highjacked!";
        }
    }
}