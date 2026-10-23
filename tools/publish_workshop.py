"""Build a mod and publish it as an update to its Steam Workshop item.

Uploads the content, title, full description and change note in one step with
SteamCMD, so nothing is copy-pasted and the game does not need to be open:

    title       <ModName> from the mod's csproj
    description assets/steam description.txt (BBCode, as shown on Steam)
    change note the "- <Version>: ..." line under [h3]Changelog[/h3] in that file
    content     a Release build in a clean staging folder (%TEMP%/Tel-Mod-Library-workshop/<ModName>),
                never the game's Mods folder, which may hold a Debug build or stale assets
    preview     thumb.png in that folder

With --description-only, only the title and description are updated: nothing is
built, and the content, preview and change note on Steam stay as they are (the
item's version and last-updated time don't change).

Publishing is refused unless the mod's files (and the shared build files) are
committed and HEAD is pushed, so the Workshop always matches GitHub.

The Workshop item is the csproj's <WorkshopID>. Tags and visibility are left as
they are on Steam: SteamCMD can't set tags, so tools/workshop_tags.py sets them
from the csproj's <Tags>. After uploading, the item is read back from the public
Steam Web API, its description compared with the local one, and its tags with the
csproj's.

With --new, a mod with no <WorkshopID> yet is published as a new item, unlisted,
and the new item's id is written into its csproj (commit that change). Then set
its tags with `python tools/workshop_tags.py --apply "<Mod Name>"`.

One-time setup: install SteamCMD (default C:\\steamcmd\\steamcmd.exe, or set
STEAMCMD) and log in once with `steamcmd +login <user> +quit` to cache the
Steam Guard approval. Without --user or STEAM_USER, the one account SteamCMD
has cached is used.

Usage:
    python tools/publish_workshop.py "<Mod Name>" [--user USER] [--description-only] [--new] [--dry-run]
"""

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
import time

from _common import MODS, REPO, build, check_built, check_pushed, clean_dir, fetch_details, read_csproj
from workshop_tags import DIFFERS, compare, steam_tags

APP_ID = "821880"
STAGING = os.path.join(tempfile.gettempdir(), "Tel-Mod-Library-workshop")
# Files outside the mod's folder that go into its build.
SHARED_PATHS = ["shared", "Directory.Build.props", "Mod.BeforeSdk.targets", "NuGet.Config"]
DEFAULT_STEAMCMD = os.environ.get("STEAMCMD", r"C:\steamcmd\steamcmd.exe")

# Steam Workshop limits.
MAX_TITLE = 128
MAX_DESCRIPTION = 8000


# SteamCMD's visibility values: 0 public, 1 friends only, 2 private, 3 unlisted.
UNLISTED = "3"


def read_props(mod_dir, new=False):
    csproj, props = read_csproj(mod_dir, optional=("WorkshopID", "Tags"))
    if new and props["WorkshopID"]:
        sys.exit("{} already has <WorkshopID> {}; publish without --new.".format(csproj, props["WorkshopID"]))
    if not new and not props["WorkshopID"]:
        sys.exit("No <WorkshopID> in {}; add the item's id from its Workshop URL, or use --new.".format(csproj))
    return csproj, props


def created_id(vdf_path):
    """The id SteamCMD writes back into the VDF after creating an item; None if it didn't."""
    with open(vdf_path, encoding="utf-8", errors="replace") as f:
        match = re.search(r'"publishedfileid"\s+"(\d+)"', f.read())
    return match.group(1) if match and match.group(1) != "0" else None


def write_workshop_id(csproj, workshop_id):
    with open(csproj, encoding="utf-8-sig") as f:
        text = f.read()
    updated, count = re.subn(r"<WorkshopID>\s*</WorkshopID>|<WorkshopID\s*/>", "<WorkshopID>{}</WorkshopID>".format(workshop_id), text)
    if count != 1:
        sys.exit("Couldn't write <WorkshopID>{}</WorkshopID> into {}; add it by hand.".format(workshop_id, csproj))
    with open(csproj, "w", encoding="utf-8", newline="") as f:
        f.write(updated)


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


def apply_command(mod):
    return 'python tools/workshop_tags.py --apply "{}"'.format(mod)


def tag_hint(mod, tags, details):
    """What to run when the item's tags on Steam differ from the csproj's; None if they match or can't be read."""
    if compare(tags, steam_tags(details)) != DIFFERS:
        return None
    return "Tags differ from the csproj; run " + apply_command(mod)


def verify(workshop_id, title, description):
    """Reads the item back and compares its title and description. Returns its details, or None if unreadable."""
    # The public API can lag the upload by a few seconds.
    for attempt in range(3):
        try:
            details = fetch_details(workshop_id)
        except Exception as e:
            print("Could not read the item back from Steam ({}); check the page manually.".format(e))
            return None
        remote = details.get("description", "").replace("\r\n", "\n").strip()
        if remote == description and details.get("title") == title:
            print("Verified: Steam shows the new title and description.")
            return details
        time.sleep(5)
    if details.get("result") != 1:
        print("Steam did not return the item (it may be private); check the page manually.")
    else:
        print("WARNING: Steam's title or description differs from the local one; check the page.")
    return details


def main():
    parser = argparse.ArgumentParser(description=(__doc__ or "").partition("\n")[0])
    parser.add_argument("mod", help="mod folder name under mods/")
    parser.add_argument("--user", default=os.environ.get("STEAM_USER"), help="Steam login (default: STEAM_USER)")
    parser.add_argument("--steamcmd", default=DEFAULT_STEAMCMD)
    parser.add_argument("--dry-run", action="store_true", help="print the upload instead of running it")
    parser.add_argument("--description-only", action="store_true",
                        help="update only the title and description; no build, content, preview or change note")
    parser.add_argument("--new", action="store_true",
                        help="publish a mod with no <WorkshopID> as a new, unlisted item and write its id into the csproj")
    args = parser.parse_args()
    if args.new and args.description_only:
        sys.exit("--new publishes everything; it can't be combined with --description-only.")

    mod_dir = os.path.join(MODS, args.mod)
    if not os.path.isdir(mod_dir):
        sys.exit("No mod folder " + mod_dir)
    csproj, props = read_props(mod_dir, args.new)
    description = read_description(mod_dir)
    note = None if args.description_only else change_note(description, props["Version"])
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
    fields = {
        "appid": APP_ID,
        "publishedfileid": "0" if args.new else props["WorkshopID"],
        "title": props["ModName"],
        "description": description,
    }
    if args.new:
        fields["visibility"] = UNLISTED
    if not args.description_only:
        # Start clean so files removed from the mod's assets don't linger.
        clean_dir(STAGING)
        build(csproj, STAGING)
        content_dir = check_staged(mod_dir, props)
        # Forward slashes keep paths free of VDF escapes; Windows accepts them.
        fields["contentfolder"] = content_dir.replace("\\", "/")
        fields["previewfile"] = os.path.join(content_dir, "thumb.png").replace("\\", "/")
        fields["changenote"] = "{}: {}".format(props["Version"], note)

    vdf_path, vdf_text = write_vdf(fields)
    try:
        what = "the description of" if args.description_only else props["Version"] + " of"
        if args.new:
            print("Publishing {} {} as a new, unlisted Workshop item".format(what, props["ModName"]))
        else:
            print("Publishing {} {} to https://steamcommunity.com/sharedfiles/filedetails/?id={}".format(
                what, props["ModName"], props["WorkshopID"]))
        if args.dry_run:
            print(vdf_text)
            return
        user = args.user or cached_user(args.steamcmd)
        if not user:
            sys.exit("No Steam login; pass --user or set STEAM_USER.")
        upload(args.steamcmd, user, vdf_path)
        if args.new:
            workshop_id = created_id(vdf_path)
            if not workshop_id:
                sys.exit("SteamCMD didn't report the new item's id; find it under your Workshop items and add it to the csproj.")
            write_workshop_id(csproj, workshop_id)
            props["WorkshopID"] = workshop_id
            print("Created https://steamcommunity.com/sharedfiles/filedetails/?id={} (unlisted); "
                  "wrote <WorkshopID> into {}. Commit and push it, then set its tags with: {}".format(
                      workshop_id, csproj, apply_command(args.mod)))
    finally:
        os.remove(vdf_path)
    details = verify(props["WorkshopID"], props["ModName"], description)
    hint = tag_hint(args.mod, json.loads(props["Tags"] or "[]"), details)
    if hint:
        print(hint)


if __name__ == "__main__":
    main()
