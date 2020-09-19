This buildpack injects a custom HttpModule that will handle all top level exceptions in a ASP.NET and MVC applications and write them to Console.

## How to use

Modify app manifest to load this buildpack, followed by [HttpModuleLoaderBuildpack](https://github.com/macsux/http-module-loader-buildpack), with final buildpack being `hwc_buildpack`.

#### Sample manifest


```yaml
applications:
- name: simpleapp
  stack: windows
  buildpacks: 
    - https://github.com/macsux/aspnet-logging-buildpack/releases/download/v0.1.0/AspNetLoggingBuildpack-win-x64-0.1.0.zip
    - https://github.com/macsux/http-module-loader-buildpack/releases/download/v0.1.0/HttpModuleLoaderBuildpack-win-x64-0.1.0.zip
    - hwc_buildpack
```

