"""Repo checks that need no game DLLs, run by CI (.github/workflows/checks.yml) and before releases.

For every maintained mod:
  - the csproj values that go into info.json are valid: <Tags> is a JSON array of
    strings and <JSONLoadOrder> a whole number (the build checks this too)
  - assets/steam description.txt has a [h3]Changelog[/h3] line for the csproj <Version>
    (a 1.0.0 mod may have no changelog yet)
  - that line fits the Workshop change-note box (100 characters after "- "); older
    lines predate the limit
  - the description has no double quote, which SteamCMD can't upload

The game's own JSON files aren't checked: the game's parser accepts unquoted keys,
which strict JSON doesn't.

Usage:
    python tools/check_repo.py
"""

import json
import os
import re
import sys

from _common import MODS, read_csproj

EXCLUDED = {"Going Viral"}  # abandoned
CHANGELOG_LIMIT = 100  # the Workshop change-note box cuts off after this many characters


def check_mod(mod_dir):
    problems = []
    _, props = read_csproj(mod_dir, optional=("Tags", "JSONLoadOrder"))
    try:
        tags = json.loads(props["Tags"] or "")
        if not isinstance(tags, list) or not all(isinstance(t, str) for t in tags):
            raise ValueError
    except ValueError:
        problems.append("<Tags> is not a JSON array of strings: {}".format(props["Tags"]))
    if not re.fullmatch(r"[+-]?\d+", props["JSONLoadOrder"] or ""):
        problems.append("<JSONLoadOrder> is not a whole number: {}".format(props["JSONLoadOrder"]))

    assets = os.path.join(mod_dir, "assets")
    names = [n for n in os.listdir(assets) if n.lower() == "steam description.txt"]
    if not names:
        return problems + ["no assets/steam description.txt"]
    with open(os.path.join(assets, names[0]), encoding="utf-8-sig") as f:
        description = f.read().replace("\r\n", "\n")
    if '"' in description:
        problems.append("steam description.txt has a double quote; SteamCMD can't upload it")
    _, found, changelog = description.partition("[h3]Changelog[/h3]")
    if not found:
        if props["Version"] != "1.0.0":
            problems.append("no [h3]Changelog[/h3] in steam description.txt")
        return problems
    match = re.search(r"^- {}: .*$".format(re.escape(props["Version"])), changelog, re.M)
    if not match:
        problems.append("no changelog line for version " + props["Version"])
    elif len(match.group(0)) - 2 > CHANGELOG_LIMIT:
        problems.append("changelog line over {} characters: {}".format(CHANGELOG_LIMIT, match.group(0)))
    return problems


def main():
    failed = False
    for name in sorted(os.listdir(MODS)):
        mod_dir = os.path.join(MODS, name)
        if name in EXCLUDED or not os.path.isdir(os.path.join(mod_dir, "assets")):
            continue
        for problem in check_mod(mod_dir):
            print("{}: {}".format(name, problem))
            failed = True
    if failed:
        sys.exit(1)
    print("All mods OK.")


if __name__ == "__main__":
    main()
