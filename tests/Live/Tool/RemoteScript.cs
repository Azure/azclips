using System.Text;

namespace Live.Tool;

/// <summary>
/// Builds the shell script that runs ON the VM via <c>az vm run-command</c>.
/// <para>
/// This stays shell, and always will: it executes inside the Linux guest, where this tool
/// does not run. It is emitted with LF endings and no BOM — the guest runs it under
/// <c>/bin/sh</c> (dash), which fails obscurely on CRLF, and that failure would only ever
/// be seen by whoever builds on Windows.
/// </para>
/// </summary>
internal static class RemoteScript
{
    public static string Build(
        string blobUrl,
        string bundleName,
        string vmName,
        string resourceGroup,
        string subscriptionId,
        string uamiClientId,
        string uamiResourceId,
        string uamiObjectId)
    {
        // User-assigned identity selectors, forwarded so the VM-side test also verifies
        // `login --identity` via --client-id/--resource-id/--object-id.
        var uami = new List<string>();
        if (!string.IsNullOrEmpty(uamiClientId))
        {
            uami.Add($"--uami-client-id '{uamiClientId}'");
        }

        if (!string.IsNullOrEmpty(uamiResourceId))
        {
            uami.Add($"--uami-resource-id '{uamiResourceId}'");
        }

        if (!string.IsNullOrEmpty(uamiObjectId))
        {
            uami.Add($"--uami-object-id '{uamiObjectId}'");
        }

        var uamiArgs = uami.Count > 0 ? " " + string.Join(' ', uami) : string.Empty;

        var lines = new[]
        {
            "# run-command executes this via /bin/sh (dash), so keep it POSIX — no pipefail.",
            "# The heavy lifting (setup-vm-mi.sh) runs under its own bash shebang.",
            "set -eu",
            "export HOME=\"${HOME:-/root}\"",
            "WORK=\"$(mktemp -d)\"",
            "cd \"$WORK\"",
            string.Empty,
            "# The bundle carries timing.sh, but it is not on disk yet — so time these two",
            "# steps by hand, emitting the same markers the orchestrator parses.",
            "export TIMING_EMIT_MARKERS=1",
            "mark() { echo \"MI_LIVE_TIMING:$1|$(( ( $(date +%s%N) - $2 ) / 1000000 ))|0\"; }",
            string.Empty,
            "echo \"==> Downloading bundle from Azure Storage\"",
            "t0=$(date +%s%N)",
            "if command -v curl >/dev/null 2>&1; then",
            $"  curl -fsSL '{blobUrl}' -o bundle.tar.gz",
            "elif command -v wget >/dev/null 2>&1; then",
            $"  wget -q -O bundle.tar.gz '{blobUrl}'",
            "else",
            "  echo \"!! neither curl nor wget available on the VM\" >&2",
            "  echo \"MI_LIVE_RESULT:FAIL\"",
            "  exit 1",
            "fi",
            "mark \"download bundle\" \"$t0\"",
            string.Empty,
            "echo \"==> Extracting bundle\"",
            "t0=$(date +%s%N)",
            "tar -xzf bundle.tar.gz",
            "mark \"extract bundle\" \"$t0\"",
            $"BUNDLE=\"{bundleName}\"",
            "chmod +x \"$BUNDLE/setup-vm-mi.sh\" 2>/dev/null || true",
            string.Empty,
            "if \"$BUNDLE/setup-vm-mi.sh\" --bundle \"$BUNDLE\" --test-mi \\",
            $"     --vm-name '{vmName}' --resource-group '{resourceGroup}' \\",
            $"     --subscription-id '{subscriptionId}'{uamiArgs}; then",
            "  echo \"MI_LIVE_RESULT:PASS\"",
            "else",
            "  echo \"MI_LIVE_RESULT:FAIL\"",
            "  exit 1",
            "fi",
        };

        // Explicit \n: Environment.NewLine would emit CRLF on a Windows host.
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Write the script without a BOM. .NET's default UTF8 encoding emits one, and the
    /// guest's /bin/sh would then see it as part of the first command.
    /// </summary>
    public static void Write(string path, string script) =>
        File.WriteAllText(path, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
