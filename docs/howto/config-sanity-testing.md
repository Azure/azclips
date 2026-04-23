# Config Sanity Testing

Use this matrix for quick local validation before or after changes to the config surface.

## Preconditions

- Start from a known config state: remove or back up `~/.cirrus/config.json`
- Use `-Force` / `--force` to avoid interactive confirmation during repeated runs
- Clear relevant environment variables before precedence checks:
    - `CIRRUS_LOG_LEVEL`
    - `PS_HTTP_MAX_RETRIES`
    - `AZURE_PS_HTTP_MAX_RETRIES`
    - `PS_HTTP_MAX_RETRIES_FOR_429`
    - `AZURE_PS_HTTP_MAX_RETRIES_FOR_429`
    - `ZIPKIN_ENDPOINT`

## CLI CRUD Matrix

| Scenario                  | Command                                            | Expected Result                               |
| ------------------------- | -------------------------------------------------- | --------------------------------------------- |
| Set string value          | `azv2 config set core.logging.level debug --force` | Value written successfully                    |
| Get string value          | `azv2 config get core.logging.level`               | Returns `debug [global config]`               |
| Set integer value         | `azv2 config set core.http.maxRetries 9 --force`   | Value written successfully                    |
| Get integer value         | `azv2 config get core.http.maxRetries`             | Returns `9 [global config]`                   |
| List explicit config only | `azv2 config list`                                 | Shows only user-set keys                      |
| List with defaults        | `azv2 config list --all`                           | Shows user-set keys plus defaults             |
| List as JSON              | `azv2 config list --all --json`                    | Valid JSON output                             |
| Delete key                | `azv2 config delete core.logging.level --force`    | Key removed successfully                      |
| Get deleted key           | `azv2 config get core.logging.level`               | Returns default value with `[default]` source |
| Schema info               | `azv2 config info`                                 | Valid keys listed                             |

## PowerShell CRUD Matrix

| Scenario                  | Command                                                        | Expected Result                    |
| ------------------------- | -------------------------------------------------------------- | ---------------------------------- |
| Import module             | `Import-Module ./src/PS/Config/Config.psd1 -Force`             | Module imports without error       |
| Set string value          | `Set-CirrusConfig -Key core.logging.level -Value debug -Force` | Value written successfully         |
| Get string value          | `Get-CirrusConfig -Key core.logging.level`                     | Returns `debug` from global config |
| Set integer value         | `Set-CirrusConfig -Key core.http.maxRetries -Value 9 -Force`   | Value written successfully         |
| List explicit config only | `Get-CirrusConfigList`                                         | Shows only user-set keys           |
| List with defaults        | `Get-CirrusConfigList -IncludeDefaults`                        | Shows explicit keys plus defaults  |
| List as JSON              | `Get-CirrusConfigList -IncludeDefaults -AsJson`                | Valid JSON output                  |
| Delete key                | `Remove-CirrusConfig -Key core.logging.level -Force`           | Key removed successfully           |
| Schema info               | `Get-CirrusConfigInfo`                                         | Schema metadata returned           |

## Precedence Matrix

Current precedence is:

`command args > environment variables > global config > defaults`

| Scenario                    | Setup                                                   | Check                                                 | Expected Result                        |
| --------------------------- | ------------------------------------------------------- | ----------------------------------------------------- | -------------------------------------- |
| Default only                | Ensure key not present in config and env var unset      | `azv2 config get core.logging.level`                  | Returns default value `info [default]` |
| Global overrides default    | `azv2 config set core.logging.level debug --force`      | `azv2 config get core.logging.level`                  | Returns `debug [global config]`        |
| Env overrides global        | Set `CIRRUS_LOG_LEVEL=error`; keep global value `debug` | `azv2 config get core.logging.level`                  | Returns `error [environment]`          |
| Defaults namespace resolves | `azv2 config set defaults.group my-rg --force`          | command/test reads `ResolveDefaultParameter("group")` | Returns `my-rg`                        |

## Negative / Validation Checks

| Scenario                 | Command                                             | Expected Result                                          |
| ------------------------ | --------------------------------------------------- | -------------------------------------------------------- |
| Unknown key rejected     | `azv2 config set made.up.key value --force`         | Validation failure unless schema intentionally allows it |
| Invalid enum rejected    | `azv2 config set core.logging.level noisy --force`  | Validation failure                                       |
| Invalid integer rejected | `azv2 config set core.http.maxRetries nope --force` | Validation failure                                       |

## Fast Regression Commands

```powershell
dotnet build cirrus.slnx -c Release -v minimal
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release -v minimal
```
