import copy
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch
spec = importlib.util.spec_from_file_location("release", Path(__file__).parents[1] / "tools/deploy_native_core.py")
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)

class DeploymentBoundaryTests(unittest.TestCase):
    def config(self):
        return {"services": {
            "rules-core-public": {"image":"release","environment":{"RulesCore__ApiSurface":"PublicOnly"},"networks":{release.BACKEND:{}}},
            "rules-core-private":{"image":"release","environment":{"RulesCore__ApiSurface":"PrivateOnly"},"networks":{"rules-core-private-tunnel":{},"dorks-and-dice-tool-control-plane":{}}}},
            "networks":{"rules-core-private-tunnel":{"name":"pair"},"dorks-and-dice-tool-control-plane":{"name":"control"}}}

    def test_valid_release(self):
        self.assertEqual(("pair","control"), release.validate_compose(self.config()))

    def test_rejects_ports_mounts_network_broadening_and_image_drift(self):
        variants=[]
        for field,value in [("ports",["8080:8080"]),("volumes",["/data:/app/data"]),("image","other")]:
            config=self.config()
            config["services"]["rules-core-private"][field]=value
            variants.append(config)
        config=self.config()
        config["services"]["rules-core-private"]["networks"][release.BACKEND]={}
        variants.append(config)
        config=self.config()
        config["services"]["rules-core-private"]["environment"]["RulesCore__ApiSurface"]="PublicOnly"
        variants.append(config)
        config=self.config()
        config["services"]["postgres"]={}
        variants.append(config)
        config=self.config()
        config["services"]["rules-core-public"]["environment"]["RulesCore__PublicDistribution__Packages__first-party-001__Enabled"]="true"
        variants.append(config)
        for config in variants:
            with self.subTest(config=config), self.assertRaises(RuntimeError):
                release.validate_compose(config)

    def test_reviewed_fingerprint_is_required_and_not_a_static_release_constant(self):
        fingerprint = "a" * 64
        with patch.dict(release.os.environ, {}, clear=True), self.assertRaises(RuntimeError):
            release.reviewed_fingerprint()
        for malformed in ("", "a" * 63, "A" * 64, "not-a-digest"):
            with self.subTest(value=malformed), patch.dict(
                release.os.environ, {"RULES_CORE_EXPECTED_FINGERPRINT": malformed}, clear=True
            ), self.assertRaises(RuntimeError):
                release.reviewed_fingerprint()
        for valid in (fingerprint, "b" * 64):
            with patch.dict(
                release.os.environ, {"RULES_CORE_EXPECTED_FINGERPRINT": valid}, clear=True
            ):
                self.assertEqual(valid, release.reviewed_fingerprint())

    def test_release_configuration_requires_reviewed_sha_and_fingerprint_before_docker(self):
        env = {"RELEASE_SHA": "c" * 40, "RULES_CORE_ENV_FILE": "/not/used"}
        with patch.dict(release.os.environ, env, clear=True), patch.object(release, "command") as run:
            with self.assertRaises(RuntimeError):
                release.release_config()
            run.assert_not_called()

    def test_catalog_log_fingerprint_must_match_reviewed_digest(self):
        expected = "a" * 64
        other = "b" * 64
        release.require_catalog_fingerprint("Native catalog fingerprint " + expected, expected)
        for log in ("", "Native catalog fingerprint " + other,
                    "Native catalog fingerprint " + expected +
                    "\\nNative catalog fingerprint " + other):
            with self.subTest(log=log), self.assertRaises(RuntimeError):
                release.require_catalog_fingerprint(log, expected)

    def test_image_must_match_commit_and_be_unprivileged(self):
        sha = "c" * 40
        image = {"Config": {"User": "1654",
                            "Labels": {"org.opencontainers.image.revision": sha}}}
        release.require_image_provenance(image, sha)
        for bad in (
            {"Config": {"User": "root", "Labels": {"org.opencontainers.image.revision": sha}}},
            {"Config": {"User": "1654", "Labels": {"org.opencontainers.image.revision": "d" * 40}}},
            {"Config": {"User": "1654", "Labels": {}}},
        ):
            with self.subTest(image=bad), self.assertRaises(RuntimeError):
                release.require_image_provenance(bad, sha)

    def test_release_workflow_is_one_click_choice_not_text_inputs(self):
        workflow = (Path(__file__).parents[1] / ".github/workflows/deploy.yml"
                    ).read_text(encoding="utf-8")
        self.assertIn("  workflow_dispatch:", workflow)
        self.assertNotIn("  workflow_run:", workflow)
        self.assertIn("deployment_type:", workflow)
        self.assertIn("type: choice", workflow)
        self.assertIn("- Content update", workflow)
        self.assertIn("- Full deploy", workflow)
        self.assertNotIn("      release_sha:", workflow)
        self.assertNotIn("      review_reference:", workflow)
        self.assertIn("RELEASE_SHA: ${{ github.sha }}", workflow)
        self.assertIn("Matching main CI succeeded for reviewed SHA", workflow)
        self.assertIn("RULES_CORE_EXPECTED_FINGERPRINT=", workflow)
        self.assertIn("org.opencontainers.image.revision=$RELEASE_SHA", workflow)
        self.assertIn("Dockerfile.content-update", workflow)
        self.assertIn("tools/deploy_native_core.py content-base", workflow)

    def test_content_only_requires_unchanged_runtime_from_prior_commit(self):
        prior = "a" * 40
        current = "b" * 40
        with patch.object(release, "command") as run, patch.object(release.subprocess, "run") as diff:
            diff.return_value.returncode = 0
            release.require_runtime_unchanged(prior, current)
            self.assertEqual(2, run.call_count)
            diff.assert_called_once()
            args = diff.call_args.args[0]
            self.assertEqual(args[:5], ["git", "diff", "--quiet", prior, current])
            self.assertIn("src/RulesCore.Host/", args)
            self.assertIn("Dockerfile", args)
            self.assertNotIn("data/rulesets/", args)

            diff.return_value.returncode = 1
            with self.assertRaisesRegex(RuntimeError, "Full deploy"):
                release.require_runtime_unchanged(prior, current)
            with self.assertRaisesRegex(RuntimeError, "Full deploy"):
                release.require_runtime_unchanged("unknown", current)

    def test_content_base_must_match_both_running_core_containers(self):
        previous = "a" * 40
        image_id = "sha256:" + "b" * 64
        tag = "dorks-and-dice-rules-core:rewrite-" + previous
        state = {
            "Image": image_id,
            "State": {"Running": True, "OOMKilled": False},
            "Config": {"Labels": {"com.docker.compose.project": release.PUBLIC}},
        }
        image = {"Id": image_id, "Config": {
            "User": "1654", "Labels": {"org.opencontainers.image.revision": previous}}}
        def inspect(kind, identifier):
            self.assertEqual(kind, "container" if identifier in
                             (release.PUBLIC, release.PRIVATE) else "image")
            if identifier in (release.PUBLIC, release.PRIVATE):
                return state
            self.assertIn(identifier, (image_id, tag))
            return image
        with patch.object(release, "inspect", side_effect=inspect), patch.object(
            release, "require_runtime_unchanged"
        ) as compatible, patch.dict(release.os.environ, {"RELEASE_SHA": "c" * 40}):
            self.assertEqual(tag, release.content_base_image())
            compatible.assert_called_once_with(previous, "c" * 40)

        with patch.object(release, "inspect", side_effect=[
            state, {**state, "Image": "sha256:" + "d" * 64}
        ]), self.assertRaisesRegex(RuntimeError, "Full deploy"):
            release.content_base_image()

    def test_content_only_dockerfile_replaces_old_native_content(self):
        contents = (Path(__file__).parents[1] / "Dockerfile.content-update"
                    ).read_text(encoding="utf-8")
        self.assertIn("FROM ${RULES_CORE_BASE_IMAGE}", contents)
        self.assertIn("rm -rf /app/data/rulesets/first-party-001/native", contents)
        self.assertIn("COPY data/rulesets/first-party-001/native/", contents)
        self.assertIn("USER $APP_UID", contents)
        self.assertNotIn("dotnet publish", contents)

    def test_unknown_private_peer_is_rejected(self):
        with self.assertRaises(RuntimeError):
            release.validate_members("pair",{"Containers":{"1":{"Name":"unrelated"}}},{release.PRIVATE,release.WIKI})

    def test_empty_pair_is_valid_before_first_start(self):
        self.assertEqual(set(), release.validate_members("pair",{"Containers":{}},{release.PRIVATE,release.WIKI}))

    def test_withdrawal_does_not_stop_a_different_release(self):
        with patch.object(release,"inspect",side_effect=[{"Image":"candidate"},{"Image":"existing"}]), patch.object(release,"command") as run:
            release.withdraw("candidate")
            run.assert_called_once_with("docker","stop","--time","15",release.PRIVATE)

if __name__ == "__main__":
    unittest.main()
