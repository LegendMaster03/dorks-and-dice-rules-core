"""Tests for the CI gate used by manual production deployments."""

import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location(
    "check_main_ci", Path(__file__).parents[1] / "tools/check_main_ci.py")
ci = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ci)
SHA = "a" * 40
REPO = "example/rules-core"


class Clock:
    def __init__(self):
        self.now = 0

    def monotonic(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds


def run(*, status="completed", conclusion="success", sha=SHA,
        branch="main", event="push", attempt=1):
    return {"id": 1, "head_sha": sha, "head_branch": branch, "event": event,
            "status": status, "conclusion": conclusion,
            "created_at": "2026-11-19T23:00:00Z", "run_attempt": attempt}


class MainCiGateTests(unittest.TestCase):
    def check(self, replies, **kwargs):
        clock = Clock()
        responses = iter(replies)
        requests = []

        def fetch(repo, sha, token):
            requests.append((repo, sha, token))
            return next(responses)

        ci.wait_for_main_ci(REPO, SHA, "test-token", fetch=fetch,
                            monotonic=clock.monotonic, sleep=clock.sleep,
                            max_wait_seconds=30, poll_seconds=10, **kwargs)
        return requests, clock.now

    def test_immediately_accepts_successful_main_push_for_exact_sha(self):
        requests, elapsed = self.check([[run()]])
        self.assertEqual([(REPO, SHA, "test-token")], requests)
        self.assertEqual(0, elapsed)

    def test_waits_for_queued_and_running_ci(self):
        _, elapsed = self.check([[run(status="queued", conclusion=None)],
                                 [run(status="in_progress", conclusion=None)],
                                 [run()]])
        self.assertEqual(20, elapsed)

    def test_waits_for_main_run_to_appear(self):
        _, elapsed = self.check([[], [run()]])
        self.assertEqual(10, elapsed)

    def test_rejects_completed_failed_canceled_and_skipped_ci(self):
        for conclusion in ("failure", "cancelled", "skipped", None):
            with self.subTest(conclusion=conclusion), self.assertRaisesRegex(
                RuntimeError, "deployment denied"
            ):
                self.check([[run(conclusion=conclusion)]])

    def test_unrelated_success_does_not_authorize_deploy(self):
        unrelated = [run(sha="b" * 40), run(branch="feature"), run(event="pull_request")]
        with self.assertRaisesRegex(RuntimeError, "Timed out"):
            self.check([unrelated] * 4)

    def test_later_failed_attempt_overrides_prior_success(self):
        with self.assertRaisesRegex(RuntimeError, "concluded 'failure'"):
            self.check([[run(), run(conclusion="failure", attempt=2)]])

    def test_unknown_status_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, "Unexpected CI status"):
            self.check([[run(status="unknown", conclusion=None)]])

    def test_invalid_identity_or_token_is_rejected_before_network(self):
        for repo, sha, token in (("../../other", SHA, "token"),
                                 (REPO, "not-a-sha", "token"),
                                 (REPO, SHA, "")):
            with self.subTest(repo=repo, sha=sha, token=token):
                with self.assertRaisesRegex(RuntimeError, "deployment denied"):
                    ci.wait_for_main_ci(repo, sha, token,
                                        fetch=lambda *_: self.fail("unexpected API request"))

    def test_api_request_uses_workflow_and_commit_filters(self):
        class Response(io.BytesIO):
            def __enter__(self):
                return self
            def __exit__(self, *_):
                self.close()

        with patch.object(ci, "urlopen", return_value=Response(
            json.dumps({"workflow_runs": [run()]}).encode()
        )) as urlopen:
            self.assertEqual([run()], ci.fetch_runs(REPO, SHA, "token"))
        request = urlopen.call_args.args[0]
        self.assertIn("/phase3b-native-public-api-ci.yml/runs?", request.full_url)
        self.assertIn("head_sha=" + SHA, request.full_url)
        self.assertIn("event=push", request.full_url)
        self.assertEqual("Bearer token", request.get_header("Authorization"))

    def test_malformed_api_response_fails_closed(self):
        class Response(io.BytesIO):
            def __enter__(self):
                return self
            def __exit__(self, *_):
                self.close()
        with patch.object(ci, "urlopen", return_value=Response(b"{}")):
            with self.assertRaisesRegex(RuntimeError, "no workflow_runs"):
                ci.fetch_runs(REPO, SHA, "token")


if __name__ == "__main__":
    unittest.main()
