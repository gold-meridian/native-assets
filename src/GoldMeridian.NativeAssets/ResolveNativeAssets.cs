using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace GoldMeridian.NativeAssets;

public sealed class ResolveNativeAssets : Task
{
    private const string manifest_name = "goldMeridianNativeLibraries";

    public ITaskItem[] Assets { get; set; } = [];

    public string RuntimeIdentifier { get; set; } = string.Empty;

    public string RuntimeGraphPath { get; set; } = string.Empty;

    public string DepsFilePath { get; set; } = string.Empty;

    [Output]
    public ITaskItem[] ResolvedAssets { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            if (!string.IsNullOrEmpty(DepsFilePath))
            {
                UpdateManifest();
            }
            else
            {
                Resolve();
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, true);
            return false;
        }
    }

    private void Resolve()
    {
        var assets =
            Assets.GroupBy(x => $"{x.GetMetadata("LibraryName")}/{x.GetMetadata("Rid")}/{x.GetMetadata("NativeFileName")}", StringComparer.Ordinal)
                  .Select(x => x.First()).ToArray();

        // In cases where a RuntimeIdentifier is provided, filter to just
        // natives that fit within the graph.
        if (!string.IsNullOrEmpty(RuntimeIdentifier))
        {
            if (JsonNode.Parse(File.ReadAllText(RuntimeGraphPath))?["runtimes"] is not JsonObject graph)
            {
                throw new InvalidDataException("The RID graph does not contain a runtimes object");
            }

            var fallbacks = new List<string> { RuntimeIdentifier };
            var visited = new HashSet<string>(fallbacks, StringComparer.Ordinal);
            for (var i = 0; i < fallbacks.Count; i++)
            {
                if (graph[fallbacks[i]]?["#import"] is not JsonArray imports)
                {
                    continue;
                }

                foreach (var import in imports.Select(x => x?.GetValue<string>()))
                {
                    // Null RID fallbacks shouldn't be a thing.
                    if (import is null)
                    {
                        continue;
                    }

                    if (visited.Add(import))
                    {
                        fallbacks.Add(import);
                    }
                }
            }

            assets = assets
                    .GroupBy(x => x.GetMetadata("LibraryName"))
                    .SelectMany(
                         x =>
                         {
                             var rid = fallbacks.FirstOrDefault(y => x.Any(a => a.GetMetadata("Rid") == y));
                             return x.Where(a => a.GetMetadata("Rid") == rid);
                         }
                     ).ToArray();
        }

        ResolvedAssets = assets.Select(
            ITaskItem (asset) =>
            {
                var result = new TaskItem(asset);
                var name = asset.GetMetadata("NativeFileName");
                result.SetMetadata(
                    "TargetPath",
                    string.IsNullOrEmpty(RuntimeIdentifier)
                        ? $"runtimes/{asset.GetMetadata("Rid")}/native/{name}"
                        : name
                );
                return result;
            }
        ).ToArray();
    }

    private void UpdateManifest()
    {
        var originalText = File.ReadAllText(DepsFilePath);
        if (string.IsNullOrEmpty(originalText))
        {
            throw new InvalidDataException("The dependency manifest is null or empty");
        }

        if (JsonNode.Parse(originalText) is not JsonObject originalJson)
        {
            throw new InvalidDataException("The dependency manifest must contain a JSON object");
        }

        if (originalJson["targets"] is not JsonObject originalTargets)
        {
            throw new InvalidDataException("The dependency manifest does not contain targets");
        }

        var previous = originalJson[manifest_name] as JsonArray;
        foreach (var target in originalTargets)
        {
            var names = previous is null ? [] : previous.Select(x => x?.GetValue<string>());
            foreach (var name in names)
            {
                // Generally shouldn't be possible
                if (name is null)
                {
                    continue;
                }

                var library = target.Value?[name] as JsonObject;
                {
                    library?.Remove("native");
                    library?.Remove("runtimeTargets");
                }
            }
        }

        var ownedLibraries = new HashSet<string>(StringComparer.Ordinal);

        if (originalJson["runtimeTarget"]?["name"]?.GetValue<string>() is not { } targetName)
        {
            throw new InvalidDataException("The dependency manifest does not name a runtime target");
        }

        if (originalTargets[targetName] is not JsonObject runtimeTarget)
        {
            throw new InvalidDataException("The dependency manifest's runtime target is missing");
        }

        foreach (var group in Assets.GroupBy(x => x.GetMetadata("LibraryName")))
        {
            var libraryName = runtimeTarget.Select(x => x.Key)
                                           .FirstOrDefault(x => x.StartsWith(group.Key + "/", StringComparison.Ordinal));
            if (libraryName is null)
            {
                throw new InvalidOperationException("Native asset owner is absent from the dependency manifest: " + group.Key);
            }

            if (runtimeTarget[libraryName] is not JsonObject library)
            {
                throw new InvalidDataException("The native asset owner must be a JSON object: " + libraryName);
            }

            var entries = new JsonObject();
            foreach (var asset in group)
            {
                entries[asset.GetMetadata("TargetPath").Replace('\\', '/')] = string.IsNullOrEmpty(RuntimeIdentifier)
                    ? new JsonObject { ["rid"] = asset.GetMetadata("Rid"), ["assetType"] = "native" }
                    : new JsonObject();
            }

            library[string.IsNullOrEmpty(RuntimeIdentifier) ? "runtimeTargets" : "native"] = entries;
            ownedLibraries.Add(libraryName);
        }

        if (ownedLibraries.Count == 0 && previous is null)
        {
            return;
        }

        originalJson[manifest_name] = new JsonArray(
            ownedLibraries.OrderBy(x => x, StringComparer.Ordinal)
                          .Select(JsonNode (x) => JsonValue.Create(x)).ToArray()
        );

        var updated = originalJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (updated != originalText)
        {
            File.WriteAllText(DepsFilePath, updated);
        }
    }
}
