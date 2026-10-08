# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Azclips implementations of the X Engineering Agent engine hooks."""

from repository_tools.reviewer.policy import analysis
from repository_tools.settings import _AAZ_OUTPUT_PATTERN
from x_engineering_agent.tools.reviews.inspection import _verified_generation_source_prs

REPOSITORY = "Azure/azclips"


def analyze_review(pr, file_changes, head_repo=None, head_sha=None, generation_source_prs=None):
    return analysis.analyze_review_tools(
        REPOSITORY, pr, file_changes, head_repo=head_repo, head_sha=head_sha,
        generation_source_prs=generation_source_prs,
    )


def readiness_policy(pr, file_changes):
    return {"blocked": _ownership_block(pr, file_changes), "validation_required": False}


def _ownership_block(pr, file_changes):
    has_aaz_output = any(
        _AAZ_OUTPUT_PATTERN.search(change.get("filename") or "") for change in file_changes
    )
    sources = _verified_generation_source_prs(pr.get("body") or "") if has_aaz_output else None
    head = pr.get("head") or {}
    findings, _ = analysis._generated_ownership_check(
        REPOSITORY, pr, file_changes, file_changes,
        (head.get("repo") or {}).get("full_name"), head.get("sha"), sources,
    )
    if not findings:
        return None
    return {
        "reason": "codegen ownership preflight failed",
        "findings": [finding["summary"] for finding in findings],
    }
