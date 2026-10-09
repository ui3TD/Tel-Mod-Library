"""Helpers shared by the tools scripts: csproj reading, Release builds, git checks, Steam lookups."""

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
    return built


def git(*args):
    return subprocess.run(["git", "-C", REPO] + list(args), check=True,
                          capture_output=True, text=True).stdout.strip()


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
    data = urllib.parse.urlencode({"itemcount": 1, "publishedfileids[0]": workshop_id}).encode()
    with urllib.request.urlopen(DETAILS_API, data, timeout=30) as response:
        return json.load(response)["response"]["publishedfiledetails"][0]
