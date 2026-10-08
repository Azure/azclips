# --------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Read the configured CLIPS GitHub project and its view."""

from repository_tools.settings import CLIPS_PROJECT_DEFAULT_VIEW, CLIPS_PROJECT_NUMBER, CLIPS_PROJECT_ORGANIZATION
from x_engineering_agent.tools.github.api import github_api_graphql
from x_engineering_agent.tools.shared.content import _sanitize_value
from x_engineering_agent.tools.shared.http import (
    _bounded_pages,
    _positive_identifier,
)
from .auth import _clips_github_token


def github_clips_project(
    view_number=CLIPS_PROJECT_DEFAULT_VIEW,
    first=100,
    max_pages=5,
):
    """Read CLIPS project 957 metadata, its selected view and project items.
    """
    view_number = _positive_identifier(view_number, "view_number")
    first = max(1, min(int(first), 100))
    max_pages = _bounded_pages(max_pages)
    query = """
    query(
      $organization: String!,
      $project: Int!,
      $first: Int!,
      $after: String
    ) {
      organization(login: $organization) {
        projectV2(number: $project) {
          id
          title
          shortDescription
          url
          updatedAt
          fields(first: 100) {
            nodes {
              ... on ProjectV2FieldCommon { id name dataType }
              ... on ProjectV2SingleSelectField {
                options { id name }
              }
            }
          }
          views(first: 50) {
            nodes {
              number
              name
              layout
              filter
              groupByFields(first: 20) {
                nodes {
                  ... on ProjectV2FieldCommon { name dataType }
                }
              }
              visibleFields(first: 50) {
                nodes {
                  ... on ProjectV2FieldCommon { name dataType }
                }
              }
            }
          }
          items(first: $first, after: $after) {
            pageInfo { hasNextPage endCursor }
            nodes {
              id
              type
              content {
                ... on Issue {
                  title
                  number
                  url
                  state
                  repository { nameWithOwner }
                  assignees(first: 20) { nodes { login } }
                  labels(first: 20) { nodes { name } }
                }
                ... on PullRequest {
                  title
                  number
                  url
                  state
                  isDraft
                  repository { nameWithOwner }
                  assignees(first: 20) { nodes { login } }
                  labels(first: 20) { nodes { name } }
                }
                ... on DraftIssue {
                  title
                  body
                  assignees(first: 20) { nodes { login } }
                }
              }
              fieldValues(first: 50) {
                nodes {
                  ... on ProjectV2ItemFieldTextValue {
                    text
                    field { ... on ProjectV2FieldCommon { name } }
                  }
                  ... on ProjectV2ItemFieldNumberValue {
                    number
                    field { ... on ProjectV2FieldCommon { name } }
                  }
                  ... on ProjectV2ItemFieldDateValue {
                    date
                    field { ... on ProjectV2FieldCommon { name } }
                  }
                  ... on ProjectV2ItemFieldSingleSelectValue {
                    name
                    field { ... on ProjectV2FieldCommon { name } }
                  }
                  ... on ProjectV2ItemFieldIterationValue {
                    title
                    startDate
                    duration
                    field { ... on ProjectV2FieldCommon { name } }
                  }
                }
              }
            }
          }
        }
      }
    }
    """
    items = []
    project = None
    after = None
    pages = 0
    while pages < max_pages:
        data = github_api_graphql(
            query,
            {
                "organization": CLIPS_PROJECT_ORGANIZATION,
                "project": CLIPS_PROJECT_NUMBER,
                "first": first,
                "after": after,
            },
            token=_clips_github_token(),
            sanitize=False,
        )
        project = (data.get("organization") or {}).get("projectV2")
        if not project:
            raise RuntimeError("CLIPS project 957 was not returned")
        connection = project["items"]
        items.extend(connection.get("nodes") or [])
        pages += 1
        page_info = connection.get("pageInfo") or {}
        if not page_info.get("hasNextPage"):
            break
        after = page_info.get("endCursor")
    selected_view = next(
        (
            view
            for view in (project.get("views") or {}).get("nodes") or []
            if view.get("number") == view_number
        ),
        None,
    )
    if selected_view is None:
        raise ValueError(f"CLIPS project view {view_number} was not found")
    return _sanitize_value(
        {
            "project": {
                key: project.get(key)
                for key in (
                    "id",
                    "title",
                    "shortDescription",
                    "url",
                    "updatedAt",
                )
            },
            "view": selected_view,
            "fields": (project.get("fields") or {}).get("nodes") or [],
            "items": items,
            "pages": pages,
            "truncated": bool(
                (project.get("items") or {})
                .get("pageInfo", {})
                .get("hasNextPage")
            ),
        }
    )
