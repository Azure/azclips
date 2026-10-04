# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Azclips production-file and component ownership rules."""


def _is_clips_production_file(path):
    return path.startswith("src/") and path.endswith((
        ".cs", ".csproj", ".json", ".ps1", ".psm1", ".psd1", ".props", ".targets",
    ))


def _clips_review_component(parts):
    if len(parts) > 1 and parts[0].casefold() == "src":
        return parts[1].casefold()
    return None
