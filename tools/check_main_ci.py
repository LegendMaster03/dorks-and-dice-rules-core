"""Gate a manual release on successful main-push CI for its exact commit.

Manual deployment may start while main CI is still running. Wait for that
specific run to finish rather than incorrectly treating pending CI as failure.
No missing, failed, canceled, or unrelated run can authorize deployment.
"""

import json
import os
import re
import time
from urllib.parse import urlencode
from urllib.request import Request, urlopen

WORKFLOW = "phase3b-native-public-api-ci.yml"
MAX_WAIT_SECONDS = 600
POLL_SECONDS = 10


def fetch_runs(repo, sha, token):
    query = urlencode({"head_sha": sha, "event": "push", "per_page": 30})
    url = f"https://api.github.com/repos/{repo}/actions/workflows/{WORKFLOW}/runs?{query}"
    request = Request(url, headers={
        "Accept": "application/vnd.github+json",
        "Authorization": f"Bearer {token}",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    with urlopen(request, timeout=20) as response:
        runs = json.load(response).get("workflow_runs")
    if not isinstance(runs, list):
        raise RuntimeError("CI API returned no workflow_runs list; deployment denied")
    return runs


def wait_for_main_ci(repo, sha, token, *, fetch=fetch_runs,
                     monotonic=time.monotonic, sleep=time.sleep,
                     max_wait_seconds=MAX_WAIT_SECONDS, poll_seconds=POLL_SECONDS):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo):
        raise RuntimeError("Invalid repository identity; deployment denied")
    if not re.fullmatch(r"[0-9a-f]{40}", sha) or not token:
        raise RuntimeError("Missing release SHA or CI API credentials; deployment denied")

    deadline = monotonic() + max_wait_seconds
    while True:
        runs = [run for run in fetch(repo, sha, token)
                if isinstance(run, dict) and run.get("head_sha") == sha
                and run.get("head_branch") == "main" and run.get("event") == "push"]
        # Prefer the most recent attempt if multiple runs match. A previous
        # successful attempt does not authorize a later failed one.
        latest = max(runs, key=lambda r: (r.get("created_at") or "",
                                          r.get("run_attempt") or 0,
                                          r.get("id") or 0), default=None)
        if latest is not None:
            status = latest.get("status")
            conclusion = latest.get("conclusion")
            if status == "completed":
                if conclusion == "success":
                    print("Matching main CI succeeded for reviewed SHA")
                    return
                raise RuntimeError(
                    f"Main push CI concluded {conclusion!r} for {sha}; deployment denied")
            if status not in ("queued", "requested", "pending", "waiting", "in_progress"):
                raise RuntimeError(f"Unexpected CI status {status!r}; deployment denied")
            print(f"Main push CI for {sha} is {status}; waiting", flush=True)
        else:
            print(f"Main push CI for {sha} has not appeared; waiting", flush=True)

        remaining = deadline - monotonic()
        if remaining <= 0:
            raise RuntimeError("Timed out waiting for successful main push CI; deployment denied")
        sleep(min(poll_seconds, remaining))


if __name__ == "__main__":
    try:
        wait_for_main_ci(os.environ["REPO_NAME"], os.environ["RELEASE_SHA"],
                         os.environ["GITHUB_TOKEN"])
    except (KeyError, RuntimeError, OSError, ValueError) as exc:
        raise SystemExit(str(exc)) from exc
