"""Build the Tel Mod Library release zip: an offline copy of every maintained mod.

Each mod is built in Release into a clean staging folder (not the game's Mods
folder), so the zip holds exactly what a fresh build deploys: the DLL, info.json
and the mod's assets. The zip has one top-level folder per mod, named after its
<ModName>, ready to drop into the game's Mods folder.

Mods in EXCLUDED (abandoned ones) are left out.

Output: releases/Tel.Mod.Library.<yyyy.mm.dd>.zip (releases/ is git-ignored).

With --upload, also creates the GitHub release "<yyyy.mm.dd> Release" (tag
library-<yyyy.mm.dd> on HEAD) with the zip attached, using the gh CLI. HEAD must
already be pushed and the repo must have no uncommitted changes, so the release
matches the code on GitHub.

Usage:
    python tools/make_release.py [--date yyyy.mm.dd] [--no-build] [--upload]
"""

import argparse
import datetime
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile

from _common import MODS, REPO, build, check_built, check_pushed, clean_dir, read_csproj

OUT_DIR = os.path.join(REPO, "releases")
# Outside the repo: OneDrive holds synced folders open, so they can't be deleted between runs.
STAGING = os.path.join(tempfile.gettempdir(), "Tel-Mod-Library-release")
EXCLUDED = {"Going Viral"}
GH_FALLBACK = r"C:\Program Files\GitHub CLI\gh.exe"


def zip_entry(name, is_dir):
    # A fixed date, so the same build gives a byte-identical zip.
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = ((0o40755 << 16) | 0x10) if is_dir else (0o100644 << 16)
    return info


def write_zip(zip_path, names):
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for name in sorted(names):
            top = os.path.join(STAGING, name)
            for root, dirs, files in os.walk(top):
                dirs.sort()
                rel_root = os.path.relpath(root, STAGING).replace(os.sep, "/")
                z.writestr(zip_entry(rel_root + "/", True), b"")
                for f in sorted(files):
                    with open(os.path.join(root, f), "rb") as data:
                        z.writestr(zip_entry(rel_root + "/" + f, False), data.read())


def find_gh():
    gh = shutil.which("gh") or (GH_FALLBACK if os.path.isfile(GH_FALLBACK) else None)
    if not gh:
        sys.exit("gh CLI not found; install it or put it on PATH.")
    return gh


def release_notes(mods):
    lines = ["Offline copy of all Tel mods. Unzip into the game's Mods folder "
             "(%AppData%\\..\\LocalLow\\Glitch Pitch\\Idol Manager\\Mods).", ""]
    lines += ["- {} {}".format(props["ModName"], props["Version"]) for _, _, props in mods]
    return "\n".join(lines)


def upload(gh, date, commit, zip_path, notes):
    tag = "library-" + date
    exists = subprocess.run([gh, "release", "view", tag], cwd=REPO, capture_output=True)
    if exists.returncode == 0:
        sys.exit("A release tagged {} already exists; delete it or pass another --date.".format(tag))
    subprocess.run([gh, "release", "create", tag, zip_path, "--target", commit,
                    "--title", date + " Release", "--notes", notes], cwd=REPO, check=True)


def main():
    parser = argparse.ArgumentParser(description=(__doc__ or "").partition("\n")[0])
    parser.add_argument("--date", default=datetime.date.today().strftime("%Y.%m.%d"),
                        help="date in the zip name (default: today)")
    parser.add_argument("--no-build", action="store_true",
                        help="zip the existing staging folder without rebuilding")
    parser.add_argument("--upload", action="store_true",
                        help="create the GitHub release with the zip attached")
    args = parser.parse_args()
    if not re.fullmatch(r"\d{4}\.\d{2}\.\d{2}", args.date):
        sys.exit("--date must look like 2024.07.11")
    # Check before building so a long build isn't wasted.
    if args.upload:
        gh = find_gh()
        commit = check_pushed()

    mods = []
    for name in sorted(os.listdir(MODS)):
        mod_dir = os.path.join(MODS, name)
        if name in EXCLUDED or not os.path.isdir(os.path.join(mod_dir, "assets")):
            continue
        csproj, props = read_csproj(mod_dir)
        mods.append((mod_dir, csproj, props))

    if not args.no_build:
        # Start clean so files removed from a mod's assets don't linger.
        clean_dir(STAGING)
        for i, (_, csproj, props) in enumerate(mods, 1):
            print("[{}/{}] Building {} {}".format(i, len(mods), props["ModName"], props["Version"]), flush=True)
            build(csproj, STAGING, quiet=True)
    for mod_dir, _, props in mods:
        check_built(mod_dir, props, STAGING)

    names = [props["ModName"] for _, _, props in mods]
    unexpected = set(os.listdir(STAGING)) - set(names)
    if unexpected:
        sys.exit("Unexpected folders in staging: " + ", ".join(sorted(unexpected)))

    zip_path = os.path.join(OUT_DIR, "Tel.Mod.Library.{}.zip".format(args.date))
    write_zip(zip_path, names)
    print("\nWrote {} ({:.1f} MB) with {} mods:".format(
        zip_path, os.path.getsize(zip_path) / 1e6, len(mods)))
    for _, _, props in mods:
        print("  {} {}".format(props["ModName"], props["Version"]))

    if args.upload:
        print("\nCreating GitHub release library-{} on {}".format(args.date, commit[:7]))
        upload(gh, args.date, commit, zip_path, release_notes(mods))


if __name__ == "__main__":
    main()
