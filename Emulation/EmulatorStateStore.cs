using System.Security.Cryptography;
using System.Text;

namespace AllaganPocket.Emulation;

internal sealed class EmulatorStateStore
{
    private const long MaximumStateBytes = 512L * 1024L * 1024L;
    private readonly string stateDirectory;
    private readonly string stateStem;

    public EmulatorStateStore(string emulatorRoot, string romPath, string? coreId = null)
    {
        stateDirectory = Path.Combine(emulatorRoot, "states");
        Directory.CreateDirectory(stateDirectory);

        var name = SafeFilePart(Path.GetFileNameWithoutExtension(romPath), "game");
        var core = SafeFilePart(coreId, string.Empty);

        var identity = Path.GetFullPath(romPath).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..12];
        stateStem = core.Length == 0 ? $"{name}-{hash}" : $"{name}-{hash}-{core}";
    }

    public string AutoPath => Path.Combine(stateDirectory, $"{stateStem}.auto.state");

    public string SlotPath(int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(stateDirectory, $"{stateStem}.slot{slot}.state");
    }

    public bool HasAuto => File.Exists(AutoPath);

    public bool HasSlot(int slot) => File.Exists(SlotPath(slot));

    public DateTime? SlotTimestamp(int slot) => Timestamp(SlotPath(slot));

    public DateTime? AutoTimestamp => Timestamp(AutoPath);

    public byte[] ReadAuto() => ReadState(AutoPath);

    public byte[] ReadSlot(int slot) => ReadState(SlotPath(slot));

    public void WriteAuto(ReadOnlySpan<byte> state) => WriteAtomically(AutoPath, state);

    public void WriteSlot(int slot, ReadOnlySpan<byte> state) => WriteAtomically(SlotPath(slot), state);

    private static DateTime? Timestamp(string path) =>
        File.Exists(path) ? File.GetLastWriteTime(path) : null;

    private static byte[] ReadState(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var length = stream.Length;
        if (length <= 0)
        {
            throw new InvalidDataException("The save state is empty.");
        }
        if (length > MaximumStateBytes)
        {
            throw new InvalidDataException("The save state is too large to load safely.");
        }

        var data = new byte[(int)length];
        stream.ReadExactly(data);
        return data;
    }

    private static void WriteAtomically(string path, ReadOnlySpan<byte> state)
    {
        if (state.IsEmpty)
        {
            throw new InvalidDataException("The save state is empty.");
        }
        if (state.Length > MaximumStateBytes)
        {
            throw new InvalidDataException("The save state is too large to store safely.");
        }

        var temporary = TemporaryPathFor(path);
        try
        {
            File.WriteAllBytes(temporary, state);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string TemporaryPathFor(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        return Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Save-state slots range from 1 to 10.");
        }
    }

    private static string SafeFilePart(string? value, string fallback)
    {
        var safe = value ?? string.Empty;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
