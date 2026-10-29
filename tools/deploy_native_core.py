#!/usr/bin/env python3
"""Commit-pinned native release; reject unsafe state before deployment."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time

FINGERPRINT = "870ca5e89a97b9176f0a3b55fb6cb9467227d13562a83dd7d72ecee5a57ecda6"
PUBLIC = "dorks-and-dice-rules-core"
PRIVATE = PUBLIC + "-private"
SITE = "dorks-and-dice-site"
WIKI = "dorks-and-dice-rules-wiki"
BACKEND = "dorks-and-dice-backend"

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

def command(*args, env=None):
    result = subprocess.run(args, text=True, capture_output=True, env=env)
    # Compose errors may contain expanded secrets; do not publish their text.
    require(result.returncode == 0, f"Command failed: {args[0]} {args[1]} (exit {result.returncode}); inspect privately")
    return result.stdout

def inspect(kind, name):
    return json.loads(command("docker", kind, "inspect", name))[0]

def validate_compose(config):
    services = config["services"]
    require(set(services) == {"rules-core-public", "rules-core-private"}, "Unexpected service set")
    private, public = services["rules-core-private"], services["rules-core-public"]
    require(private.get("environment", {}).get("RulesCore__ApiSurface") == "PrivateOnly", "Private mode is incorrect")
    require(public.get("environment", {}).get("RulesCore__ApiSurface") == "PublicOnly", "Public mode is incorrect")
    require(set(private.get("networks", {})) == {"rules-core-private-tunnel", "dorks-and-dice-tool-control-plane"}, "Private Core network broadening")
    require(set(public.get("networks", {})) == {BACKEND}, "Public Core network mismatch")
    for service in services.values():
        require(not service.get("ports"), "Host ports are prohibited")
        require(not service.get("volumes"), "Use embedded native data without mounts")
        require(not any("PublicDistribution" in key for key in service.get("environment", {})), "Public distribution configuration is prohibited")
    require(private.get("image") == public.get("image"), "Ingress image drift")
    return config["networks"]["rules-core-private-tunnel"]["name"], config["networks"]["dorks-and-dice-tool-control-plane"]["name"]

def validate_members(name, network, allowed):
    members = {item["Name"] for item in network.get("Containers", {}).values()}
    require(members <= allowed, f"Unexpected member on {name}; review privately")
    return members

def release_config():
    sha = os.environ.get("RELEASE_SHA", "")
    require(bool(re.fullmatch(r"[0-9a-f]{40}", sha)), "RELEASE_SHA must be an exact commit")
    env_file = os.environ.get("RULES_CORE_ENV_FILE", "")
    require(Path(env_file).is_file(), "Core deployment .env is missing")
    image = "dorks-and-dice-rules-core:rewrite-" + sha
    env = {**os.environ, "RULES_CORE_IMAGE": image}
    compose = ["docker", "compose", "--project-name", PUBLIC, "--env-file", env_file,
               "-f", "docker-compose.yml", "-f", "docker-compose.private-tunnel.yml"]
    config = json.loads(command(*compose, "config", "--format", "json", env=env))
    pair, control = validate_compose(config)
    require(len({pair, control, BACKEND}) == 3, "Pair/control/backend networks must be distinct")
    return image, env, compose, pair, control

def preflight():
    image, env, compose, pair, control = release_config()
    validate_members(pair, inspect("network", pair), {PRIVATE, WIKI})
    members = validate_members(control, inspect("network", control), {SITE, PRIVATE})
    require(SITE in members, "Site is missing from the restricted control plane")
    site = inspect("container", SITE)
    require(site["State"]["Running"], "Site is not running")
    require(BACKEND in site["NetworkSettings"]["Networks"], "Site is missing from the tool backend")
    site_env = dict(item.split("=", 1) for item in site["Config"].get("Env", []) if "=" in item)
    grants = [value for key, value in site_env.items() if key.startswith("ToolHosting__PrivateTunnels__rules-wiki__")]
    require("rules-core" in grants, "Site environment does not establish rules-wiki -> rules-core; verify installed policy")
    names = set(command("docker", "ps", "-a", "--format", "{{.Names}}").splitlines())
    for name in (PUBLIC, PRIVATE):
        if name in names:
            current = inspect("container", name)
            require(current["Config"].get("Labels", {}).get("com.docker.compose.project") == PUBLIC, "Core name belongs to a different Compose project")
    print("Host preflight passed: isolation, Site grant and container ownership", flush=True)
    return image, env, compose, pair, control

def probe(network, host, path):
    output = command("docker", "run", "--rm", "--network", network, "curlimages/curl:8.12.1",
                     "--connect-timeout", "2", "--max-time", "5", "-sS", "-w", "\\n%{http_code}",
                     f"http://{host}:8080{path}")
    return output.rsplit("\n", 1)

def verify(image_id, pair, control):
    for name, networks in [(PUBLIC, {BACKEND}), (PRIVATE, {pair, control})]:
        state = inspect("container", name)
        require(state["Image"] == image_id, "Running image differs from release")
        require(state["State"]["Running"] and not state["State"].get("OOMKilled"), "Core exited or was OOM-killed")
        require(set(state["NetworkSettings"]["Networks"]) == networks, "Running network mismatch")
        require(not state["HostConfig"].get("PortBindings"), "Running Core publishes host ports")
    checks = [(BACKEND, PUBLIC, "/ready", "200"), (pair, PRIVATE, "/ready", "200"),
              (BACKEND, PUBLIC, "/api/private/rulesets", "404"), (pair, PRIVATE, "/api/rulesets", "404"),
              (pair, PRIVATE, "/api/private/rulesets", "401"), (BACKEND, PUBLIC, "/api/rules", "404"),
              (BACKEND, PUBLIC, "/api/rulesets/first-party-001/rules", "404")]
    for network, host, path, expected in checks:
        _, status = probe(network, host, path)
        require(status == expected, f"Ingress failed: {host}{path} expected {expected}")
    body, status = probe(BACKEND, PUBLIC, "/api/rulesets")
    require(status == "200" and json.loads(body) == {"rulesets": []}, "Anonymous distribution is not closed")
    for name in (PUBLIC, PRIVATE):
        # JsonConsole writes catalog initialization messages to stdout.
        require(FINGERPRINT in command("docker", "logs", name), "Catalog fingerprint mismatch")
    validate_members(pair, inspect("network", pair), {PRIVATE, WIKI})
    validate_members(control, inspect("network", control), {SITE, PRIVATE})

def withdraw(image_id):
    for name in (PRIVATE, PUBLIC):
        try:
            current = inspect("container", name)
        except RuntimeError:
            continue
        if current["Image"] == image_id:
            command("docker", "stop", "--time", "15", name)

def deploy():
    image, env, compose, pair, control = preflight()
    built = inspect("image", image)
    image_id = built["Id"]
    require(built["Config"].get("User") not in (None, "", "root", "0"), "Release image must be unprivileged")
    try:
        # Never remove orphans, recreate databases or touch another Compose project.
        command(*compose, "up", "-d", "--no-build", "--no-deps", "rules-core-public", "rules-core-private", env=env)
        for attempt in range(30):
            try:
                statuses = [probe(network, name, "/ready")[1]
                            for network, name in ((BACKEND, PUBLIC), (pair, PRIVATE))]
                if statuses == ["200", "200"]:
                    break
            except RuntimeError:
                pass
            if attempt == 29:
                raise RuntimeError("Native catalog did not become ready")
            time.sleep(2)
        # Security/identity failures below withdraw immediately, without retrying.
        verify(image_id, pair, control)
        before = {name: inspect("container", name)["RestartCount"] for name in (PUBLIC, PRIVATE)}
        for _ in range(6):
            time.sleep(10)
            verify(image_id, pair, control)
            require(all(inspect("container", name)["RestartCount"] == count for name, count in before.items()), "Core restarted during observation")
        print(json.dumps({"status": "infrastructure-smoke-passed", "imageId": image_id, "fingerprint": FINGERPRINT,
                          "observationSeconds": 60, "liveSiteTicketAcceptance": "pending"}), flush=True)
    except Exception:
        withdraw(image_id)
        raise

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in {"preflight", "deploy"}, "Expected preflight or deploy")
        preflight() if sys.argv[1] == "preflight" else deploy()
    except (RuntimeError, KeyError, ValueError, OSError) as error:
        print(f"Native Core release stopped: {error}", file=sys.stderr)
        sys.exit(1)
