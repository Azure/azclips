using System.Diagnostics;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// Cross-process mutex over a lock file, semantics copied from
/// <c>ConfigurationService.AcquireMutationLockAsync</c>. Kept here as a
/// standalone helper because that method is <c>private</c> to config —
/// duplicating the small routine is preferable to widening a public API
/// solely for arm-cache to consume.
///
/// <para>Only refreshers hold the lock; readers don't (they may see a
/// half-written file if we crash mid-write, which is why the refresher
/// writes to <c>tmp</c> and atomically renames).</para>
/// </summary>
internal static class FileLockHelper
{
    /// <summary>
    /// Blocks up to 10 s waiting for <paramref name="lockPath"/> to become
    /// available (<see cref="FileShare.None"/>). Returns <c>null</c> if the
    /// wait times out — caller treats that as "someone else is refreshing;
    /// skip". The returned stream must be disposed to release the lock.
    /// </summary>
    public static async Task<FileStream?> TryAcquireAsync(
        string lockPath,
        CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
