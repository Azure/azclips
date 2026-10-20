// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

namespace Core.Cli;

internal sealed class ExtensionFileSnapshot
{
    private readonly string _name;
    private readonly string _version;
    private readonly long _length;
    private readonly DateTime _lastWriteTimeUtc;

    public ExtensionFileSnapshot(string name, string version, string path, string ownerId)
    {
        _name = name;
        _version = version;
        Location = Path.GetFullPath(path);
        var file = ReadFile();
        _length = file.Length;
        _lastWriteTimeUtc = file.LastWriteTimeUtc;
        var canonicalPath = CanonicalizePath(Location);
        CacheKey = string.Join(":",
            ownerId,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(canonicalPath)),
            _length.ToString(CultureInfo.InvariantCulture),
            _lastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
    }

    public string Location { get; }
    public string CacheKey { get; }

    internal static string CanonicalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }

    public void ValidateUnchanged()
    {
        var file = ReadFile();
        if (file.Length != _length || file.LastWriteTimeUtc != _lastWriteTimeUtc)
        {
            throw new InvalidOperationException(
                $"Selected extension '{_name}' version '{_version}' at '{Location}' has changed. Restart the host to rediscover extensions.");
        }
    }

    private FileInfo ReadFile()
    {
        var file = new FileInfo(Location);
        if (!file.Exists)
        {
            throw new FileNotFoundException(
                $"Selected extension '{_name}' version '{_version}' at '{Location}' is no longer available.", Location);
        }
        return file;
    }
}
