# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Run this package's engine hooks through the X Engineering Agent broker."""

from pathlib import Path

import pytest

from x_engineering_agent.repository import testing

REPOSITORY = "Azure/azclips"
PACKAGE = Path(__file__).resolve().parents[1]


@pytest.fixture(autouse=True)
def pinned(monkeypatch, tmp_path):
    testing.pin(monkeypatch, tmp_path, testing.local(PACKAGE, REPOSITORY))


def hook(name, *args, **kwargs):
    return testing.hook(REPOSITORY, name, *args, **kwargs)


PR = {"title": "Fix", "body": "", "head": {"sha": "a" * 40, "repo": {"full_name": "Azure/azclips"}}}


def test_review_treats_source_as_production():
    changes = [{"filename": "src/Clips.Core/Auth.cs", "status": "modified", "patch": "@@ -1 +1 @@\n+x"}]
    result = hook("analyze_review", PR, changes, head_repo="Azure/azclips", head_sha="a" * 40)
    assert result["repository"] == REPOSITORY
    assert {check["tool"] for check in result["checks"]} >= {"release-artifact", "generated-ownership"}
    assert any(target["tool"] == "test-strength" for target in result["review_targets"])


def test_readiness_blocks_hand_edited_generated_code():
    changes = [{"filename": "src/Clips.Core/Client.generated.cs", "status": "modified", "patch": "+x"}]
    assert hook("readiness_policy", PR, changes)["blocked"]["reason"] == "codegen ownership preflight failed"
    plain = [{"filename": "src/Clips.Core/Auth.cs", "status": "modified", "patch": "+x"}]
    assert hook("readiness_policy", PR, plain) == {"blocked": None, "validation_required": False}


def test_no_tester_or_title_hooks():
    with pytest.raises(LookupError):
        hook("live_test_plan", [])
    with pytest.raises(LookupError):
        hook("title_failure_plan", PR, [])


def test_definitions_reference_existing_engine_helpers_and_tools():
    assert testing.definition_problems(testing.local(PACKAGE, REPOSITORY)) == []
