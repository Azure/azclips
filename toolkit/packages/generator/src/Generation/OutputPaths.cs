namespace Ace.Generator;

/// <summary>
/// Normalizes and validates generator output paths. Output paths are always
/// <em>relative</em> to the caller's base directory: the generator is filesystem-
/// agnostic and never knows (or joins) the base directory itself — that is the
/// handler's job. This keeps the whole generated output a single relocatable
/// folder tree.
/// </summary>
internal static class OutputPaths
{
    /// <summary>
    /// Normalize <paramref name="path"/> to a clean, <c>/</c>-separated relative
    /// path. Fails (with <paramref name="error"/> set) if the path is empty,
    /// absolute, or escapes the base directory via <c>..</c>.
    /// </summary>
    public static bool TryNormalize(string path, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "the path is empty";
            return false;
        }

        string unified = path.Replace('\\', '/');
        if (unified.StartsWith('/') || System.IO.Path.IsPathRooted(unified))
        {
            error = $"'{path}' is absolute; output paths must be relative to the base directory";
            return false;
        }

        var segments = new List<string>();
        foreach (string segment in unified.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                error = $"'{path}' escapes the base directory ('..' is not allowed)";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            error = $"'{path}' resolves to an empty relative path";
            return false;
        }

        normalized = string.Join('/', segments);
        return true;
    }
}
