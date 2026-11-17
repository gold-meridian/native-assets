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

To create an additional output name (e.g., for a versioned library), you can add an alias during build:

```xml
<!-- Removes `.0` since it's detected as the extension. -->
<NativeAsset Update="runtimes\*\native\*.so.0" Alias="%(Filename)"/>

<!-- Removes `.0` by manually replacing it. -->
<NativeAsset Update="runtimes\*\native\*.0.dylib"
             Alias="$([System.String]::Copy('%(Filename)').Replace('.0', '')).dylib"/>
```

`Alias`ing the native asset, as demonstrated here, means it will be copied to the output with both the original file name and the new, aliased name.

The file name and RID can also be manually overridden:

```xml
<!-- Keep the source file name and choose its RID explicitly. -->
<NativeAsset Include="vendor\windows\helper.dll"
             Rid="win-x64"/>

<!-- Infer the RID from the conventional path, but output only libFAudio.so. -->
<NativeAsset Include="runtimes\linux-x64\native\libFAudio.so.0"
             NativeFileName="libFAudio.so"/>

<!-- Neither the source path nor its file name needs to match the output. -->
<NativeAsset Include="vendor\audio\linux-release.bin"
             Rid="linux-x64" NativeFileName="libFAudio.so"/>
```

Doing so will not produce duplicate files.
