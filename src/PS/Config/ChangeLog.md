<!-- markdownlint-disable MD022 MD032 MD041 -->

<!--
    Please leave this section at the top of the change log.

    Changes for the upcoming release should go under the section titled "Upcoming Release", and should adhere to the following format:

    ## Upcoming Release
    * Overview of change #1
        - Additional information about change #1
    * Overview of change #2
        - Additional information about change #2

    ## Version X.Y.Z
    * Overview of change #1
        - Additional information about change #1
-->
## Upcoming Release

* List effective configuration including built-in defaults with `Get-AzConfig` and `Get-AzConfigList`.
    - Defaults are now included automatically; `-IncludeDefaults` has been removed. Use `-ConfiguredOnly` to show only file and environment values.
    - `Get-AzConfig` no longer requires `-Key`; keyed and section reads retain their existing behavior.

## Version 0.1.0

* First preview