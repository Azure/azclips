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

* `Get-AzAccessToken`: Add cmdlet to acquire an access token for a resource, well-known resource type, or explicit scope list from the current signed-in context
    - Token output is protected by default; set the `AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN` environment variable to emit the raw token as plain text

## Version 0.1.0

* First preview