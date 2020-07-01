# CompilerPaths MSBuild Task

The CompilerPaths Task is an attempt to help discover compiler paths using MSBuild. A [NuGet package](https://www.nuget.org/packages/CompilerPaths) is available.

## APIs

APIs follow these rules:

- Have consistent prefix (i.e. `MSVC`).
- Contain absolute paths.
- Optional argument have reasonable defaults (e.g. no version means latest).

### `MSVCFindCompilerPaths` Target

Supports Visual Studio 15.0 and above. Only supports Windows 10 SDKs.

Inputs:

- `MSVCPlatform`: The target platform.
- `MSVCVSVersion`: **Optional** Target Visual Studio version.
- `MSVCWinSDKVersion`: **Optional** Target Windows SDK version.

Outputs:

- `MSVCCompilerPath`: Property containing the absolute path to compiler.
- `MSVCIncludePaths`: Item containing absolute paths to be used as `include` paths.
- `MSVCLibPaths`: Item containing absolute paths to be used as `lib` paths.

Example:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <PropertyGroup>
    <MSVCPlatform>x64</MSVCPlatform>
    <MSVCVSVersion>16.6.30204.135</MSVCVSVersion>
    <MSVCWinSDKVersion>10.0.17738.0</MSVCWinSDKVersion>
  </PropertyGroup>

  <Target Name="UseMSVC" BeforeTargets="CoreCompile" DependsOnTargets="MSVCFindCompilerPaths">
    <Message Text="MSVCCompilerPath: $(MSVCCompilerPath)" Importance="High"/>
    <Message Text="MSVCIncludePaths: @(MSVCIncludePaths)" Importance="High"/>
    <Message Text="MSVCLibPaths: @(MSVCLibPaths)" Importance="High"/>
  </Target>

  <ItemGroup>
    <PackageReference Include="CompilerPaths" Version="1.*" />
  </ItemGroup>
</Project>
```

## References

[MSBuild Reference](https://docs.microsoft.com/visualstudio/msbuild/msbuild)

[MSBuild Transforms](https://docs.microsoft.com/visualstudio/msbuild/msbuild-transforms)

[Writing an MSBuild Task](https://docs.microsoft.com/visualstudio/msbuild/task-writing)