namespace AllaganPocket.Emulation;

internal sealed class RomLibrary
{
    private readonly string romDirectory;

    public RomLibrary(string emulatorRoot)
    {
        romDirectory = Path.Combine(emulatorRoot, "roms");
        Directory.CreateDirectory(romDirectory);
        foreach (var system in EmulatorSystemCatalog.All)
        {
            Directory.CreateDirectory(Path.Combine(romDirectory, system.Id));
        }
    }

    public IReadOnlyList<RomEntry> Scan(IEnumerable<string>? additionalDirectories = null,
        IEnumerable<RomFileRecord>? explicitFiles = null)
    {
        var found = new Dictionary<string, RomEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in EmulatorSystemCatalog.All)
        {
            AddDirectory(Path.Combine(romDirectory, system.Id), true, found, system);
        }

        AddDirectory(romDirectory, false, found, null);
        if (additionalDirectories is not null)
        {
            foreach (var directory in additionalDirectories)
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    AddDirectory(directory, true, found, null);
                }
            }
        }

        if (explicitFiles is not null)
        {
            foreach (var record in explicitFiles)
            {
                AddFile(record.Path, record.SystemId, found);
            }
        }

        var result = found.Values.ToList();
        result.Sort(static (left, right) => string.Compare(left.Title, right.Title,
            StringComparison.CurrentCultureIgnoreCase));
        return result;
    }


    private static void AddFile(string path, string? systemId, Dictionary<string, RomEntry> found)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var forcedSystem = string.IsNullOrWhiteSpace(systemId)
                ? null
                : EmulatorSystemCatalog.ById(systemId);
            var system = forcedSystem?.Supports(fullPath) == true
                ? forcedSystem
                : EmulatorSystemCatalog.ResolveWithFolderHint(fullPath);
            if (system is not null)
            {
                found[fullPath] = new RomEntry(fullPath, system);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            EmulatorLog.Warning($"[Allagan Retro Pocket] Could not add ROM '{path}': {exception.Message}");
        }
    }

    private static void AddDirectory(string directory, bool recursive, Dictionary<string, RomEntry> found,
        EmulatorSystemDefinition? forcedSystem)
    {
        if (!Directory.Exists(directory)) return;
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            foreach (var path in Directory.EnumerateFiles(directory, "*", options))
            {
                var fullPath = Path.GetFullPath(path);
                var system = forcedSystem?.Supports(fullPath) == true
                    ? forcedSystem
                    : EmulatorSystemCatalog.ResolveWithFolderHint(fullPath);
                if (system is not null)
                {
                    found[fullPath] = new RomEntry(fullPath, system);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EmulatorLog.Warning($"[Allagan Retro Pocket] Could not scan '{directory}': {exception.Message}");
        }
    }
}
