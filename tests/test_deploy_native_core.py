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
