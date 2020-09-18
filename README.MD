This project offers simplified way of creating Cloud Foundry buildpacks that install HttpModules into .NET framework apps that run in IIS and use `hwc_buildpack`. 


## Quick Start

This is a template project that can be used by DotNet CLI to boostrap a new buildpack. Install it into CLI via

```shell
dotnet new -i CloudFoundry.Buildpack.V2.HttpModule
```

The embedded build scripts rely on Git to do versioning through the use of [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning). This means that you need to initialize a git repo, initialize versioning via `nbgv` CLI and create at least one commit

```shell
dotnet tool install -g nbgv
nbgv install
git init
git add .
git commit -m Initial
```

#### How to implement

Implement your module by modifying `AspNetLoggingBuildpackHttpModule` class. 

## How to package

Included build packaging is based on Nuke.Build project. Either use the included build shell scripts, or install IDE plugins to run included targets. Get full list of useful targets and parameters with `--help` argument for the build script. 


### Compiling

```powershell
.\build.ps1 --stack windows
```

Final output will be placed in `/artifacts` folder

## How to Release

Use the embedded `Release` target to create and publish buildpack as asset to GitHub releases. This requires you have remote repo set and have specified API Key as parameter (or set via environmental variable).

## How to use

The buildpack can be either installed into the platform via `cf create-buildpack` option and reference in manifest by name, or (if allowed by platform settings) referenced via a URL.

The buildpacks created by this template are intended to be used in conjunction with HttpModuleLoaderBuildpack and HwcBuildpack. Module buildpacks such as the ones created by this template do not register themselves into the app - they simply supply the HttpModule and the necessary dependencies into the container. The registration of modules into the IIS pipeline is done by HttpModuleLoaderBuildpack which must be executed after all the module buildpacks have ran. HttpModuleLoaderBuildpack also ensures that there are no assembly version conflicts between multiple HttpModules and the app itself. It will take the necessary steps to ensure that multiple versions of assemblies are loaded side-by-side into the app domain. Ex. if your HttpModule uses JSON.NET v12 and the app uses JSON.NET v11, both assemblies will be loaded into the app domain and HttpModule and app will use version of the original version of the assembly they was compiled against. `hwc_buildpack` needs to be specified last in the manifest. 

#### Sample manifest


```yaml
applications:
- name: simpleapp
  stack: windows
  buildpacks: 
    - https://github.com/macsux/http-module-loader-buildpack/releases/download/v0.1.0/HttpModuleLoaderBuildpack-win-x64-0.1.0.zip
    - https://github.com/macsux/http-module-loader-buildpack/releases/download/v0.1.0/HttpModuleLoaderBuildpack-win-x64-0.1.0.zip
    - hwc_buildpack
```

