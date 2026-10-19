# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Validate the centrally held project credential without returning it."""

from x_engineering_agent.tools.github.api import require_credential


def _clips_github_token():
    return require_credential("clips")
