namespace AspNetLoggingBuildpack
{
    public abstract class SupplyBuildpack : BuildpackBase
    {
        public sealed override void Supply(string buildPath, string cachePath, string depsPath, int index)
        {
            DoApply(buildPath, cachePath, depsPath, index);
        }

        public sealed override void Finalize(string buildPath, string cachePath, string depsPath, int index)
        {
            // doesn't get called
        }

        public override void Release(string buildPath)
        {
            // does not get called
        }

        // supply buildpacks may get this lifecycle event, but since only one buildpack will be selected if detection is used, it must be final
        // therefore supply buildpacks always must reply with false
        public sealed override bool Detect(string buildPath) => false;  
    }
}