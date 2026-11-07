
import json
import os
import urllib.parse
import urllib.request
from collections import Counter
from datetime import date, datetime, time, timedelta, timezone
from pathlib import Path

repo = os.environ["REPO"]
token = os.environ["GH_TOKEN"]
api_url = os.environ["GITHUB_API_URL"].rstrip("/")
start_date = date.fromisoformat(os.environ["START_DATE"])
end_date = date.fromisoformat(os.environ["END_DATE"])

if end_date < start_date:
    raise ValueError("End date must not be earlier than start date.")

# GitHub's 'since' parameter filters by updated_at.
# Any issue created within our requested range must have updated_at
# greater than or equal to its created_at, so this safely includes it.
start_utc = datetime.combine(
    start_date, time.min, tzinfo=timezone.utc
)
end_exclusive = datetime.combine(
    end_date + timedelta(days=1), time.min, tzinfo=timezone.utc
)

counts = Counter()
page = 1

while True:
    params = urllib.parse.urlencode({
        "state": "all",
        "since": start_utc.isoformat().replace("+00:00", "Z"),
        "sort": "created",
        "direction": "asc",
        "per_page": 100,
        "page": page,
    })

    url = (
        f"{api_url}/repos/{repo}/issues?{params}"
    )

    request = urllib.request.Request(
        url,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
        },
    )

    with urllib.request.urlopen(request, timeout=30) as response:
        issues = json.loads(response.read().decode("utf-8"))

    if not issues:
        break

    for issue in issues:
        # The Issues API also returns pull requests.
        if "pull_request" in issue:
            continue

        created_at = datetime.fromisoformat(
            issue["created_at"].replace("Z", "+00:00")
        )

        if not (start_utc <= created_at < end_exclusive):
            continue

        author = issue.get("user")
        login = author.get("login") if author else None
        counts[login or "(deleted user)"] += 1

    if len(issues) < 100:
        break

    page += 1

# Sort by issue count, descending; use username for tie-breaking.
ranked = sorted(
    counts.items(),
    key=lambda item: (-item[1], item[0].lower())
)

total = sum(counts.values())
max_count = max(counts.values(), default=0)

lines = [
    "# Issue Report",
    "",
    f"- **Repository:** `{repo}`",
    f"- **Date range:** {start_date.isoformat()} to {end_date.isoformat()} (UTC)",
    f"- **Total issues:** {total}",
    f"- **Number of authors:** {len(ranked)}",
    "",
    "## Issues created per person",
    "",
]

if ranked:
    lines.extend([
        "| Author | Issues | Bar chart |",
        "|---|---:|---|",
    ])

    for author, count in ranked:
        bar_length = max(1, round(count / max_count * 30))
        bar = "█" * bar_length
        lines.append(f"| {author} | {count} | {bar} |")

    lines.extend([
        "",
        "## Ranking",
        "",
        "| Rank | Author | Issues |",
        "|---:|---|---:|",
    ])

    for rank, (author, count) in enumerate(ranked, start=1):
        lines.append(f"| {rank} | {author} | {count} |")
else:
    lines.extend([
        "No issues were created during this date range.",
    ])

lines.extend([
    "",
    "---",
    "",
    "*Generated automatically by GitHub Actions.*",
])

output = Path("reports/issue-report.md")
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text("\n".join(lines) + "\n", encoding="utf-8")

print(f"Report generated: {output}")
print(f"Total issues: {total}")
print(f"Authors: {len(ranked)}")
