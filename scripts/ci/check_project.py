#!/usr/bin/env python3
"""License-free checks for Unity files that must survive a clean checkout."""

import argparse
from collections import defaultdict
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
ASSETS = "Game/Assets/"
TEXT_EXTENSIONS = {".cs", ".meta", ".unity", ".prefab", ".asset", ".json", ".asmdef", ".inputactions", ".shader", ".hlsl", ".cginc", ".compute"}
GUID = re.compile(r"^guid: ([0-9a-fA-F]{32})\s*$", re.MULTILINE)
CONFLICT = re.compile(r"^(?:<{7} |={7}\s*$|>{7} )", re.MULTILINE)
REQUIRED = ("Game/Packages/manifest.json", "Game/Packages/packages-lock.json", "Game/ProjectSettings/ProjectVersion.txt", "Game/ProjectSettings/EditorBuildSettings.asset")


def inspect(root, paths):
    paths = set(paths)
    failures = []
    metadata = {}
    identities = defaultdict(list)
    case_paths = defaultdict(list)
    assemblies = defaultdict(list)

    def fail(path, reason):
        failures.append({"path": path, "message": reason})

    for path in REQUIRED:
        if path not in paths:
            fail(path, "Required Unity project file is not tracked.")
    for path in sorted(p for p in paths if p.startswith("Game/")):
        case_paths[path.casefold()].append(path)
        file = root / path
        if not file.is_file():
            fail(path, "Tracked file is missing from this checkout.")
            continue
        if file.suffix not in TEXT_EXTENSIONS and path not in REQUIRED:
            continue
        try:
            content = file.read_text(encoding="utf-8-sig")
        except UnicodeError:
            fail(path, "Expected UTF-8 text could not be decoded.")
            continue
        if CONFLICT.search(content):
            fail(path, "Unresolved merge conflict marker.")
        if file.suffix in {".json", ".asmdef", ".inputactions"} and not path.startswith("Game/.vscode/"):
            try:
                value = json.loads(content)
                if file.suffix == ".asmdef":
                    name = value.get("name") if isinstance(value, dict) else None
                    if not isinstance(name, str) or not name.strip():
                        fail(path, "Assembly definition has no valid name.")
                    else:
                        assemblies[name].append(path)
                if path == "Game/Packages/manifest.json":
                    dependencies = value.get("dependencies", {})
                    if "com.unity.test-framework" not in dependencies:
                        fail(path, "Unity Test Framework dependency is missing.")
            except (json.JSONDecodeError, AttributeError) as error:
                fail(path, f"Invalid JSON: {error}")
        if path.startswith(ASSETS) and path.endswith(".meta"):
            match = GUID.search(content)
            if not match:
                fail(path, "Unity metadata must contain a 32-character GUID.")
            else:
                guid = match.group(1).lower()
                metadata[path[:-5]] = guid
                identities[guid].append(path)
            asset = path[:-5]
            if asset not in paths and "folderAsset: yes" not in content:
                fail(path, "Metadata has no tracked asset; commit the asset or remove stale metadata.")
        if path == "Game/ProjectSettings/ProjectVersion.txt":
            if not re.search(r"^m_EditorVersion: \d+\.\d+\.\d+[abfp]\d+\s*$", content, re.MULTILINE):
                fail(path, "Unity editor version is missing or malformed.")

    for path in sorted(p for p in paths if p.startswith(ASSETS) and not p.endswith(".meta")):
        if path + ".meta" not in paths:
            fail(path, "Unity asset metadata is not tracked; references can break on teammates' machines.")
    for key, matches in case_paths.items():
        if len(matches) > 1:
            fail(matches[0], "Paths differ only by case: " + ", ".join(sorted(matches)))
    for guid, matches in identities.items():
        if len(matches) > 1:
            fail(matches[0], f"Duplicate Unity GUID {guid}: " + ", ".join(matches))
    for name, matches in assemblies.items():
        if len(matches) > 1:
            fail(matches[0], f"Duplicate assembly name {name}: " + ", ".join(matches))

    settings = root / "Game/ProjectSettings/EditorBuildSettings.asset"
    if settings.is_file():
        content = settings.read_text(encoding="utf-8-sig")
        for scene, guid in re.findall(r"- enabled: 1\s*\n\s*path: ([^\n]+)\n\s*guid: ([0-9a-fA-F]+)", content):
            path = "Game/" + scene.strip()
            if path not in paths:
                fail(path, "Enabled build scene is not tracked (including exact filename case).")
            elif metadata.get(path) != guid.lower():
                fail(path, "Build settings scene GUID does not match its metadata.")

    return {"checked_assets": sum(p.startswith(ASSETS) and not p.endswith(".meta") for p in paths),
            "status": "failed" if failures else "passed", "failures": failures}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, help="Save a machine-readable report")
    args = parser.parse_args()
    paths = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT, text=True).split("\0")
    result = inspect(ROOT, paths)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(f'Project integrity: {result["status"]} ({result["checked_assets"]} tracked assets)')
    for failure in result["failures"]:
        print(f'{failure["path"]}: {failure["message"]}')
    raise SystemExit(bool(result["failures"]))


if __name__ == "__main__":
    main()
