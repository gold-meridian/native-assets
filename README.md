# native-assets

> Enables NuGet-like, automatic, RID-based native library loading for project references in MSBuild.

---

Adds native assets from project references to .NET's dependency manifest for automatic, RID-based library loading.

## Usage

Install the package in the project containing native files and include them in the `NativeAsset` item:

```xml
<ItemGroup>
    <!-- CAUTION: It's important to not set PrivateAssets="all" when referring
         to the project since it needs to be carried transitively to any
         consumers of the project reference. -->
    <PackageReference Include="GoldMeridian.NativeAssets" Version="1.0.0"/>
    
    <!-- This demonstrates the simplest case of including native assets, where
         they already map cleanly to an RID. -->
    <NativeAsset Include="runtimes\*\native\*"/>
</ItemGroup>
```

It's easiest to keep files under `runtimes/<rid>/native/` so that you don't need to manually remap them at build time.

If a file path needs to be remapped or a file name needs to be changed (e.g., it's versioned), you can add an alias during build:

```xml
<!-- Removes `.0` since it's detected as the extension. -->
<NativeAsset Update="runtimes\*\native\*.so.0" Alias="%(Filename)"/>

<!-- Removes `.0` by manually replacing it. -->
<NativeAsset Update="runtimes\*\native\*.0.dylib"
             Alias="$([System.String]::Copy('%(Filename)').Replace('.0', '')).dylib"/>
```

`Alias`ing the native asset, as demonstrated here, means it will be copied to the output with both the original file name and the new, aliased name.

TODO: add support for overriding name outright, add support for manually specifying RID
