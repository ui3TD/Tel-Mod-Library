"""Update Harmony Checker to ask for a new IM-HarmonyIntegration release.

Run after each IM-HI release. Steps:

  1. Set IMHIUpdate.MIN_VERSION to the IM-HI version.
  2. Bump the mod's minor version and add a changelog line. If the current version
     isn't on Steam yet, it keeps that version and updates its changelog line instead.
  3. Build in Release (deploys to the game), run the unit tests and the in-game tests.
     If any of them fails, the three edited files are put back as they were.
  4. Commit.
  5. With --publish: push main and publish the Workshop item (tools/publish_workshop.py).
     Stops first if the live Steam description has edits the repo doesn't have, since
     publishing would overwrite them.

Usage:
    python tools/update_harmony_checker.py <imhi version> [--publish] [--skip-ingame] [--dry-run]
"""

import argparse
import os
import re
import subprocess
import sys

from _common import REPO, fetch_details, git

MOD_DIR = os.path.join(REPO, "mods", "Harmony Checker")
SOURCE = os.path.join(MOD_DIR, "Harmony Checker.cs")
CSPROJ = os.path.join(MOD_DIR, "Harmony Checker.csproj")
DESCRIPTION = os.path.join(MOD_DIR, "assets", "steam description.txt")
TESTS = os.path.join(REPO, "tests", "HarmonyChecker.Tests", "HarmonyChecker.Tests.csproj")
INGAME = os.path.join(REPO, "tests", "InGameTests.TelMods", "run.py")
PUBLISH = os.path.join(REPO, "tools", "publish_workshop.py")

MIN_VERSION_RE = re.compile(r"(MIN_VERSION = new Version\()(\d+), (\d+), (\d+)(\))")
CSPROJ_VERSION_RE = re.compile(r"(<Version>)([^<]+)(</Version>)")
WORKSHOP_ID_RE = re.compile(r"<WorkshopID>([^<]+)</WorkshopID>")
CHANGELOG_LIMIT = 100  # the Workshop change-note box cuts off after this many characters


def parse_version(text):
    if not re.fullmatch(r"\d+\.\d+\.\d+", text):
        sys.exit("Version must look like 1.2.0, not " + text)
    return tuple(int(p) for p in text.split("."))


def fmt(version):
    return ".".join(str(p) for p in version)


def read(path):
    """The file's text with LF line endings (the repo stores LF; .gitattributes)."""
    with open(path, "rb") as f:
        return f.read().decode("utf-8")


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def fetch_description(workshop_id):
    details = fetch_details(workshop_id)
    if details.get("result") != 1:
        sys.exit("Steam did not return Workshop item {}.".format(workshop_id))
    return details["description"].replace("\r\n", "\n").strip()


def normalize(text):
    return text.lstrip("\ufeff").replace("\r\n", "\n").strip()


def update_changelog(description, mod_version, imhi, released):
    """Adds or rewrites the changelog line for mod_version."""
    note = "Asks for IM-HI {} or newer.".format(imhi)
    if released:
        line = "- {}: {}".format(mod_version, note)
        if len(line) - 2 > CHANGELOG_LIMIT:
            sys.exit("Changelog line is over {} characters: {}".format(CHANGELOG_LIMIT, line))
        return description.rstrip("\r\n") + "\n" + line + "\n"
    # Unreleased: fold the new IM-HI version into the existing line.
    pattern = re.compile(r"^- " + re.escape(mod_version) + r": (.*?)\r?$", re.M)
    match = pattern.search(description)
    if not match:
        sys.exit("No changelog line for unreleased {}; add one by hand.".format(mod_version))
    text = match.group(1)
    if re.search(r"Asks for IM-HI \d+\.\d+\.\d+ or newer\.", text):
        text = re.sub(r"Asks for IM-HI \d+\.\d+\.\d+ or newer\.", note, text)
    else:
        text = text + " " + note
    if len("{}: {}".format(mod_version, text)) > CHANGELOG_LIMIT:
        sys.exit("The {} changelog line would be over {} characters; shorten it by hand:\n{}".format(
            mod_version, CHANGELOG_LIMIT, text))
    start, end = match.span(1)
    return description[:start] + text + description[end:]


def run(cmd):
    print("> " + " ".join(cmd), flush=True)
    subprocess.run(cmd, cwd=REPO, check=True)


def main():
    parser = argparse.ArgumentParser(description=(__doc__ or "").partition("\n")[0])
    parser.add_argument("imhi_version", help="the new IM-HI release, e.g. 1.3.0")
    parser.add_argument("--publish", action="store_true", help="push and publish to the Workshop")
    parser.add_argument("--skip-ingame", action="store_true", help="don't run the in-game tests")
    parser.add_argument("--dry-run", action="store_true", help="print the changes without making them")
    args = parser.parse_args()
    imhi = parse_version(args.imhi_version)

    source, csproj, description = read(SOURCE), read(CSPROJ), read(DESCRIPTION)
    min_match = MIN_VERSION_RE.search(source)
    version_match = CSPROJ_VERSION_RE.search(csproj)
    workshop_match = WORKSHOP_ID_RE.search(csproj)
    if not min_match or not version_match or not workshop_match:
        sys.exit("Couldn't find MIN_VERSION, <Version> or <WorkshopID>.")
    current_min = tuple(int(g) for g in min_match.groups()[1:4])
    if current_min >= imhi:
        print("Harmony Checker already asks for IM-HI {}; nothing to do.".format(fmt(current_min)))
        return

    if git("status", "--porcelain", "--", os.path.relpath(MOD_DIR, REPO)):
        sys.exit("mods/Harmony Checker has uncommitted changes; commit or stash them first.")
    if git("diff", "--cached", "--name-only"):
        sys.exit("Other changes are staged; commit or unstage them first.")
    if args.publish and git("rev-parse", "--abbrev-ref", "HEAD") != "main":
        sys.exit("Publishing pushes main; check out main first.")

    # Steam's description has a changelog line for every version that was published.
    workshop_id = workshop_match.group(1).strip()
    live = fetch_description(workshop_id)
    mod_version = parse_version(version_match.group(2).strip())
    released = "- {}:".format(fmt(mod_version)) in live
    # An unreleased version's changelog line is expected to be missing from Steam.
    local = normalize(description)
    if not released:
        local = re.sub(r"\n- {}: .*$".format(re.escape(fmt(mod_version))), "", local, flags=re.M)
    if args.publish and live != local:
        sys.exit("The live Workshop description differs from the repo's; publishing would overwrite "
                 "it. Bring the repo up to date with Steam first.")
    new_version = (mod_version[0], mod_version[1] + 1, 0) if released else mod_version

    new_source = MIN_VERSION_RE.sub(lambda m: m.group(1) + "{}, {}, {}".format(*imhi) + m.group(5), source, count=1)
    new_csproj = CSPROJ_VERSION_RE.sub(lambda m: m.group(1) + fmt(new_version) + m.group(3), csproj, count=1)
    new_description = update_changelog(description, fmt(new_version), fmt(imhi), released)

    print("Harmony Checker {} -> {} ({}), MIN_VERSION {} -> {}".format(
        fmt(mod_version), fmt(new_version), "on Steam, bumping" if released else "not on Steam yet, kept",
        fmt(current_min), fmt(imhi)))
    print("Changelog: " + normalize(new_description).splitlines()[-1])
    if args.dry_run:
        return

    originals = {SOURCE: source, CSPROJ: csproj, DESCRIPTION: description}
    write(SOURCE, new_source)
    write(CSPROJ, new_csproj)
    write(DESCRIPTION, new_description)
    try:
        run(["dotnet", "build", CSPROJ, "-c", "Release", "-nologo", "-v:q"])
        # HarmonyChecker.Tests crash the test host in Release; Debug passes.
        run(["dotnet", "test", TESTS, "-c", "Debug", "-nologo"])
        if not args.skip_ingame:
            run([sys.executable, INGAME, "--scope", "IM-HarmonyIntegration Plugin"])
        git("add", "--", *originals)
        git("commit", "-m", "Harmony Checker {}: ask for IM-HI {}".format(fmt(new_version), fmt(imhi)))
    except BaseException:
        # Don't leave the mod half-updated; the game's Mods folder keeps whatever was built.
        subprocess.run(["git", "-C", REPO, "reset", "--quiet", "--"] + list(originals), check=False)
        for path, text in originals.items():
            write(path, text)
        print("Failed; the edited files were put back as they were.")
        raise
    print("Committed " + git("log", "--oneline", "-1"))

    if not args.publish:
        print("Not published; rerun with --publish, or push and run tools/publish_workshop.py "
              "\"Harmony Checker\".")
        return
    run(["git", "push", "origin", "main"])
    run([sys.executable, PUBLISH, "Harmony Checker"])


if __name__ == "__main__":
    main()
