# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Read CLIPS squads and their repository contributions."""

from .auth import _clips_github_token
from repository_tools.settings import CLIPS_PROJECT_DEFAULT_VIEW, CLIPS_PROJECT_NUMBER, CLIPS_PROJECT_ORGANIZATION, CLIPS_SQUADS
from x_engineering_agent.config import INSIGHT_GITHUB_REPOSITORIES
from x_engineering_agent.tools.github.api import (
    _validate_repository,
    github_api_get,
)
from x_engineering_agent.tools.github.repositories import github_contributors
from x_engineering_agent.tools.shared.content import _sanitize_value


def github_clips_squads():
    """List the six ACT squads and their current GitHub team members."""
    squads = []
    for slug, name in CLIPS_SQUADS.items():
        result = github_api_get(
            f"/orgs/{CLIPS_PROJECT_ORGANIZATION}/teams/{slug}/members",
            params={"per_page": 100},
            max_pages=2,
            token=_clips_github_token(),
            sanitize=False,
        )
        members = sorted(
            {
                member.get("login")
                for member in result.get("data") or []
                if member.get("login")
            },
            key=str.casefold,
        )
        squads.append(
            {
                "name": name,
                "slug": slug,
                "member_count": len(members),
                "members": members,
            }
        )
    return _sanitize_value(
        {
            "project": {
                "organization": CLIPS_PROJECT_ORGANIZATION,
                "number": CLIPS_PROJECT_NUMBER,
                "default_view": CLIPS_PROJECT_DEFAULT_VIEW,
                "url": (
                    "https://github.com/orgs/"
                    f"{CLIPS_PROJECT_ORGANIZATION}/projects/"
                    f"{CLIPS_PROJECT_NUMBER}/views/"
                    f"{CLIPS_PROJECT_DEFAULT_VIEW}"
                ),
            },
            "squads": squads,
        }
    )


def github_clips_squad_contributors(repositories=None, max_pages=10):
    """Rank configured repository contributors by current CLIPS squad.
    """
    selected_repositories = repositories or list(INSIGHT_GITHUB_REPOSITORIES)
    if isinstance(selected_repositories, str):
        selected_repositories = [selected_repositories]
    selected_repositories = list(dict.fromkeys(selected_repositories))
    for repository in selected_repositories:
        _validate_repository(repository)

    squad_data = github_clips_squads()
    membership = {}
    for squad in squad_data["squads"]:
        for member in squad["members"]:
            membership.setdefault(member.casefold(), set()).add(squad["slug"])
    contributions = {
        squad["slug"]: {
            member.casefold(): {
                "login": member,
                "contributions": 0,
                "repositories": {},
            }
            for member in squad["members"]
        }
        for squad in squad_data["squads"]
    }
    for repository in selected_repositories:
        result = github_contributors(repository, max_pages=max_pages)
        for contributor in result.get("data") or []:
            login = contributor.get("login")
            if not login:
                continue
            squad_slugs = membership.get(login.casefold())
            if not squad_slugs:
                continue
            count = int(contributor.get("contributions") or 0)
            for squad_slug in squad_slugs:
                entry = contributions[squad_slug][login.casefold()]
                entry["contributions"] += count
                entry["repositories"][repository] = count

    squads = []
    for squad in squad_data["squads"]:
        contributors = sorted(
            contributions[squad["slug"]].values(),
            key=lambda item: (-item["contributions"], item["login"].casefold()),
        )
        squads.append(
            {
                **squad,
                "total_contributions": sum(
                    contributor["contributions"]
                    for contributor in contributors
                ),
                "contributors": contributors,
            }
        )
    return _sanitize_value(
        {
            "project": squad_data["project"],
            "repositories": selected_repositories,
            "squads": squads,
        }
    )
