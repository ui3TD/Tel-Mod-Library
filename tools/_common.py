"""Helpers shared by the tools scripts: csproj reading, Release builds, git checks, Steam lookups."""

import getpass
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.parse
import urllib.request

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODS = os.path.join(REPO, "mods")
DETAILS_API = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/"


def read_csproj(mod_dir, optional=()):
    """Returns (csproj path, props). <ModName> and <Version> are required; names in optional are None if missing."""
    projects = glob.glob(os.path.join(mod_dir, "*.csproj"))
    if len(projects) != 1:
        sys.exit("Expected one .csproj in " + mod_dir)
    with open(projects[0], encoding="utf-8-sig") as f:
        text = f.read()
    props = {}
    for name in ("ModName", "Version") + tuple(optional):
        match = re.search(r"<{0}>([^<]+)</{0}>".format(name), text)
        if not match and name in ("ModName", "Version"):
            sys.exit("No <{}> in {}".format(name, projects[0]))
        props[name] = match.group(1).strip() if match else None
    return projects[0], props


CHANGELOG_HEADING = "[h3]Changelog[/h3]"
CHANGELOG_RECENT = 5  # the changelog keeps this many newest entries, plus every major version
CHANGELOG_LINE_RE = re.compile(r"^- (\d+)\.(\d+)\.(\d+): ")


def changelog_version(line):
    """Returns (major, minor, patch) of a "- <version>: ..." changelog line, or None."""
    match = CHANGELOG_LINE_RE.match(line)
    return tuple(int(g) for g in match.groups()) if match else None


def is_major(version):
    """1.0.0, 2.0.0, ...; 0.x versions are pre-release, not major versions."""
    return version[0] >= 1 and version[1:] == (0, 0)


def trim_changelog(lines):
    """Sorts changelog lines newest first and keeps the newest CHANGELOG_RECENT plus every major version."""
    ordered = sorted(lines, key=changelog_version, reverse=True)
    return [line for i, line in enumerate(ordered) if i < CHANGELOG_RECENT or is_major(changelog_version(line))]


def clean_dir(path):
    if os.path.exists(path):
        shutil.rmtree(path)
    os.makedirs(path)


def build(csproj, out_dir, quiet=False):
    """Builds in Release and deploys into out_dir instead of the game's Mods folder."""
    # ModOutputDir is a global property here, so it overrides Directory.Build.props.
    subprocess.run(["dotnet", "build", csproj, "-c", "Release", "-nologo"] + (["-v:q"] if quiet else [])
                   + ["-p:ModOutputDir=" + out_dir], check=True)


def check_built(mod_dir, props, out_dir):
    """Exits unless out_dir/<ModName> holds the csproj's version, its DLL and every asset. Returns that folder."""
    built = os.path.join(out_dir, props["ModName"])
    info_path = os.path.join(built, "info.json")
    if not os.path.isfile(info_path):
        sys.exit("Build did not produce " + info_path)
    with open(info_path, encoding="utf-8-sig") as f:
        info = json.load(f)
    if info.get("Version") != props["Version"]:
        sys.exit("{} info.json is version {}, csproj is {}.".format(
            props["ModName"], info.get("Version"), props["Version"]))
    dll = os.path.join(built, info["HarmonyID"] + ".dll")
    if not os.path.isfile(dll):
        sys.exit("No DLL at " + dll)
    assets = os.path.join(mod_dir, "assets")
    for root, _, files in os.walk(assets):
        for name in files:
            rel = os.path.relpath(os.path.join(root, name), assets)
            if not os.path.isfile(os.path.join(built, rel)):
                sys.exit("{}: asset {} missing from the build".format(props["ModName"], rel))
    check_machine_neutral(built)
    return built


def check_machine_neutral(folder):
    """Exits if any file under folder holds data specific to this PC."""
    user = getpass.getuser()
    pattern = re.compile(r"(?<![A-Za-z]){}(?![A-Za-z])".format(re.escape(user)).encode(), re.IGNORECASE)
    wide = [s.encode("utf-16-le") for s in (user, user.lower(), user.capitalize())]
    for root, _, files in os.walk(folder):
        for name in files:
            path = os.path.join(root, name)
            with open(path, "rb") as f:
                data = f.read()
            if pattern.search(data) or any(w in data for w in wide):
                sys.exit("{} holds data specific to this PC; refusing to publish it.".format(path))


def git(*args):
    return subprocess.run(["git", "-C", REPO] + list(args), check=True,
                          capture_output=True, text=True).stdout.strip()


def check_committed(paths=()):
    """Exits unless paths (default: the whole repo) are committed."""
    if git("status", "--porcelain", "--", *paths):
        sys.exit("Uncommitted changes in {}; commit them first.".format(", ".join(paths) or "the repo"))


def check_pushed(paths=()):
    """Exits unless paths (default: the whole repo) are committed and HEAD is on origin. Returns HEAD."""
    if git("status", "--porcelain", "--", *paths):
        sys.exit("Uncommitted changes in {}; commit and push them first.".format(", ".join(paths) or "the repo"))
    git("fetch", "--quiet", "origin")
    if not git("branch", "-r", "--contains", "HEAD"):
        sys.exit("HEAD is not pushed to origin; push it first.")
    return git("rev-parse", "HEAD")


def fetch_details(workshop_id):
    """The Workshop item's details from the public Steam Web API (result 1 = found)."""
    return fetch_details_many([workshop_id])[workshop_id]


def fetch_details_many(workshop_ids):
    """Each Workshop item's details from the public Steam Web API in one call, by id (result 1 = found)."""
    fields = {"itemcount": len(workshop_ids)}
    fields.update({"publishedfileids[{}]".format(i): wid for i, wid in enumerate(workshop_ids)})
    with urllib.request.urlopen(DETAILS_API, urllib.parse.urlencode(fields).encode(), timeout=30) as response:
        items = json.load(response)["response"]["publishedfiledetails"]
    return {item["publishedfileid"]: item for item in items}
