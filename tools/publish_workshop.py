"""Build a mod and publish it as an update to its Steam Workshop item.

Uploads the content, title, full description and change note in one step with
SteamCMD, so nothing is copy-pasted and the game does not need to be open:

    title       <ModName> from the mod's csproj
    description assets/steam description.txt (BBCode, as shown on Steam)
    change note the "- <Version>: ..." line under [h3]Changelog[/h3] in that file
    content     a Release build in a clean staging folder (%TEMP%/Tel-Mod-Library-workshop/<ModName>),
                never the game's Mods folder, which may hold a Debug build or stale assets
    preview     thumb.png in that folder

Publishing is refused unless the mod's files (and the shared build files) are
committed and HEAD is pushed, so the Workshop always matches GitHub.

The Workshop item is the csproj's <WorkshopID>. Tags and visibility are left as
they are on Steam. After uploading, the item is read back from the public Steam
Web API and its description compared with the local one.

One-time setup: install SteamCMD (default C:\\steamcmd\\steamcmd.exe, or set
STEAMCMD) and log in once with `steamcmd +login <user> +quit` to cache the
Steam Guard approval. Without --user or STEAM_USER, the one account SteamCMD
has cached is used.

Usage:
    python tools/publish_workshop.py "<Mod Name>" [--user USER] [--dry-run]
"""

import argparse
import os
import re
import subprocess
import sys
import tempfile
import time

from _common import MODS, REPO, build, check_built, check_pushed, clean_dir, fetch_details, read_csproj

APP_ID = "821880"
STAGING = os.path.join(tempfile.gettempdir(), "Tel-Mod-Library-workshop")
# Files outside the mod's folder that go into its build.
SHARED_PATHS = ["shared", "Directory.Build.props", "Mod.BeforeSdk.targets", "NuGet.Config"]
DEFAULT_STEAMCMD = os.environ.get("STEAMCMD", r"C:\steamcmd\steamcmd.exe")

# Steam Workshop limits.
MAX_TITLE = 128
MAX_DESCRIPTION = 8000


def read_props(mod_dir):
    csproj, props = read_csproj(mod_dir, optional=("WorkshopID",))
    if not props["WorkshopID"]:
        sys.exit("No <WorkshopID> in {}; add the item's id from its Workshop URL.".format(csproj))
    return csproj, props


def read_description(mod_dir):
    assets = os.path.join(mod_dir, "assets")
    names = [n for n in os.listdir(assets) if n.lower() == "steam description.txt"]
    if not names:
        sys.exit("No steam description.txt in " + assets)
    with open(os.path.join(assets, names[0]), encoding="utf-8-sig") as f:
        return f.read().replace("\r\n", "\n").strip()


def change_note(description, version):
    _, found, changelog = description.partition("[h3]Changelog[/h3]")
    if not found:
        sys.exit("No [h3]Changelog[/h3] section in the steam description.")
    match = re.search(r"^- {}:\s*(.+)$".format(re.escape(version)), changelog, re.MULTILINE)
    if not match:
        sys.exit("No '- {}: ...' line in the changelog; add one before publishing.".format(version))
    return match.group(1).strip()


def vdf_string(value):
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def write_vdf(fields):
    lines = ['"workshopitem"', "{"]
    lines += ["\t{} {}".format(vdf_string(k), vdf_string(v)) for k, v in fields.items()]
    lines.append("}")
    fd, path = tempfile.mkstemp(suffix=".vdf", prefix="workshop_")
    with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    return path, "\n".join(lines)


def check_staged(mod_dir, props):
    staged = check_built(mod_dir, props, STAGING)
    if not os.path.isfile(os.path.join(staged, "thumb.png")):
        sys.exit("No thumb.png in " + staged)
    return staged


def cached_user(steamcmd):
    """The account SteamCMD has a cached login for, if it has exactly one."""
    try:
        with open(os.path.join(os.path.dirname(steamcmd), "config", "config.vdf"), encoding="utf-8", errors="replace") as f:
            text = f.read()
    except OSError:
        return None
    block = re.search(r'"Accounts"\s*\{(.*?)\n\t*\}', text, re.S)
    names = re.findall(r'^\s*"([^"]+)"\s*\n\s*\{', block.group(1), re.M) if block else []
    return names[0] if len(names) == 1 else None


def upload(steamcmd, user, vdf_path):
    if not os.path.isfile(steamcmd):
        sys.exit("SteamCMD not found at {}; install it or set STEAMCMD.".format(steamcmd))
    # stdin stays attached so SteamCMD can ask for a password or Steam Guard code if the login has expired.
    proc = subprocess.Popen([steamcmd, "+login", user, "+workshop_build_item", vdf_path, "+quit"],
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace")
    assert proc.stdout is not None
    output = []
    for line in proc.stdout:
        print(line, end="")
        output.append(line)
    proc.wait()
    text = "".join(output)
    if "ERROR" in text or "Success." not in text:
        sys.exit("SteamCMD did not report success; the Workshop item may not have been updated.")


def verify(workshop_id, title, description):
    # The public API can lag the upload by a few seconds.
    for attempt in range(3):
        try:
            details = fetch_details(workshop_id)
        except Exception as e:
            print("Could not read the item back from Steam ({}); check the page manually.".format(e))
            return
        remote = details.get("description", "").replace("\r\n", "\n").strip()
        if remote == description and details.get("title") == title:
            print("Verified: Steam shows the new title and description.")
            return
        time.sleep(5)
    if details.get("result") != 1:
        print("Steam did not return the item (it may be private); check the page manually.")
    else:
        print("WARNING: Steam's title or description differs from the local one; check the page.")


def main():
    parser = argparse.ArgumentParser(description=(__doc__ or "").partition("\n")[0])
    parser.add_argument("mod", help="mod folder name under mods/")
    parser.add_argument("--user", default=os.environ.get("STEAM_USER"), help="Steam login (default: STEAM_USER)")
    parser.add_argument("--steamcmd", default=DEFAULT_STEAMCMD)
    parser.add_argument("--dry-run", action="store_true", help="print the upload instead of running it")
    args = parser.parse_args()

    mod_dir = os.path.join(MODS, args.mod)
    if not os.path.isdir(mod_dir):
        sys.exit("No mod folder " + mod_dir)
    csproj, props = read_props(mod_dir)
    description = read_description(mod_dir)
    note = change_note(description, props["Version"])
    if len(props["ModName"]) > MAX_TITLE:
        sys.exit("Title is over {} characters.".format(MAX_TITLE))
    if len(description) > MAX_DESCRIPTION:
        sys.exit("Description is {} characters; Steam allows {}.".format(len(description), MAX_DESCRIPTION))

    # Check before building so a build isn't wasted; a dry run only warns.
    try:
        check_pushed([os.path.relpath(mod_dir, REPO)] + SHARED_PATHS)
    except SystemExit as e:
        if not args.dry_run:
            raise
        print("WARNING: " + str(e))
    # Start clean so files removed from the mod's assets don't linger.
    clean_dir(STAGING)
    build(csproj, STAGING)
    content_dir = check_staged(mod_dir, props)

    # Forward slashes keep paths free of VDF escapes; Windows accepts them.
    vdf_path, vdf_text = write_vdf({
        "appid": APP_ID,
        "publishedfileid": props["WorkshopID"],
        "contentfolder": content_dir.replace("\\", "/"),
        "previewfile": os.path.join(content_dir, "thumb.png").replace("\\", "/"),
        "title": props["ModName"],
        "description": description,
        "changenote": "{}: {}".format(props["Version"], note),
    })
    try:
        print("Publishing {} {} to https://steamcommunity.com/sharedfiles/filedetails/?id={}".format(
            props["ModName"], props["Version"], props["WorkshopID"]))
        if args.dry_run:
            print(vdf_text)
            return
        user = args.user or cached_user(args.steamcmd)
        if not user:
            sys.exit("No Steam login; pass --user or set STEAM_USER.")
        upload(args.steamcmd, user, vdf_path)
    finally:
        os.remove(vdf_path)
    verify(props["WorkshopID"], props["ModName"], description)


if __name__ == "__main__":
    main()
