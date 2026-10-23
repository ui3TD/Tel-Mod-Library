"""Compare each mod's Steam Workshop tags with its csproj <Tags>, and set them on Steam.

The csproj's <Tags> is the only place a mod's tags are set; the build writes them into
info.json. SteamCMD can't set tags, so --apply sets them from inside the game, which is
logged in to Steam as Idol Manager: it starts an IM-InGameTests probe session on the main
menu with no mods and no save, and runs a probe that calls Steamworks (StartItemUpdate,
SetItemTags, SubmitItemUpdate with no change note). Only the tags change. The title,
description, content, preview, visibility and version on Steam stay as they are.

Items that aren't public (unlisted, private) can't be read from the public Steam Web API,
so their tags can't be compared. --apply-all leaves them out; name them with --apply and
check the item's page afterwards.

Needs IM-InGameTests next to this repo, or its folder in the IM_INGAMETESTS environment
variable. --apply refuses a mod whose csproj isn't committed and pushed (the rest of
the repo may have unpushed commits).

Usage:
    python tools/workshop_tags.py                          # compare every mod with Steam; changes nothing
    python tools/workshop_tags.py --apply "<Mod Name>"     # set that mod's tags on Steam (repeatable)
    python tools/workshop_tags.py --apply-all              # set the tags of every mod whose tags differ
    python tools/workshop_tags.py --apply-all --dry-run    # print what would be sent
"""

import argparse
import json
import os
import subprocess
import sys
import tempfile

from _common import MODS, REPO, fetch_details_many, git, read_csproj

INGAMETESTS = os.environ.get("IM_INGAMETESTS", os.path.join(os.path.dirname(REPO), "IM-InGameTests"))
PROBE_DIR = os.path.join(tempfile.gettempdir(), "Tel-Mod-Library-workshop-tags")
PROBE_METHOD = "WorkshopTags.Run"
# How long the probe waits for Steam's answer to each item, in seconds.
STEAM_WAIT = 120

MATCHES = "matches"
DIFFERS = "differs"
UNREADABLE = "can't read from the public API"


class Mod:
    def __init__(self, name, csproj, workshop_id, tags):
        self.name = name
        self.csproj = csproj
        self.workshop_id = workshop_id
        self.tags = tags


def read_mods():
    """Every maintained mod with a <WorkshopID>, sorted by name."""
    mods = []
    for name in sorted(os.listdir(MODS)):
        mod_dir = os.path.join(MODS, name)
        if not os.path.isdir(mod_dir):
            continue
        csproj, props = read_csproj(mod_dir, optional=("WorkshopID", "Tags"))
        if props["WorkshopID"]:
            mods.append(Mod(name, csproj, props["WorkshopID"], json.loads(props["Tags"] or "[]")))
    return mods


def steam_tags(details):
    """The item's tags as Steam shows them, or None if the public API didn't return the item."""
    if not details or details.get("result") != 1:
        return None
    return [t["tag"] for t in details.get("tags", [])]


def compare(local, remote):
    """MATCHES, DIFFERS or UNREADABLE. Steam keeps its own tag order, so order is ignored, and so is case."""
    if remote is None:
        return UNREADABLE
    return MATCHES if sorted(t.casefold() for t in local) == sorted(t.casefold() for t in remote) else DIFFERS


def show_tags(tags):
    return "(unknown)" if tags is None else ", ".join(tags) if tags else "(none)"


def print_table(rows):
    """rows: (mod name, csproj tags, Steam tags or None, status)."""
    table = [("Mod", "Tags in csproj", "Tags on Steam", "")]
    table += [(name, show_tags(local), show_tags(remote), "" if status == MATCHES else status.upper() if status == DIFFERS else status)
              for name, local, remote, status in rows]
    widths = [max(len(row[i]) for row in table) for i in range(3)]
    for row in table:
        print(("  ".join(cell.ljust(width) for cell, width in zip(row, widths)) + "  " + row[3]).rstrip())


def choose(mods, statuses, names, apply_all):
    """The mods to send: named ones unless their tags already match, or with --apply-all every mod that differs."""
    if apply_all:
        return [m for m in mods if statuses[m.name] == DIFFERS]
    by_name = {m.name: m for m in mods}
    unknown = [n for n in names if n not in by_name]
    if unknown:
        sys.exit("No maintained mod with a <WorkshopID> named: " + ", ".join(unknown))
    chosen = []
    for name in names:
        if statuses[name] == MATCHES:
            print("{}: the tags on Steam already match the csproj; nothing to send.".format(name))
        else:
            chosen.append(by_name[name])
    return chosen


def cs_string(value):
    # JSON string escapes are valid C# string escapes.
    return json.dumps(value)


def probe_source(mods):
    """A probe whose WorkshopTags.Run sets each mod's tags and records Steam's answer under the item id."""
    ids = ", ".join("{}UL".format(m.workshop_id) for m in mods)
    tags = ", ".join("new[] {{ {} }}".format(", ".join(cs_string(t) for t in m.tags)) if m.tags else "new string[0]"
                     for m in mods)
    return """using InGameTests;
using Steamworks;
using System.Collections;
using UnityEngine;

// Generated by Tel-Mod-Library/tools/workshop_tags.py. Sets only the tags of each item.
internal static class WorkshopTags
{{
    private static readonly ulong[] Ids = {{ {ids} }};
    private static readonly string[][] Tags = {{ {tags} }};

    private static IEnumerator Run(TestContext ctx)
    {{
        AppId_t app = SteamUtils.GetAppID();
        for (int i = 0; i < Ids.Length; i++)
        {{
            string key = Ids[i].ToString();
            UGCUpdateHandle_t handle = SteamUGC.StartItemUpdate(app, new PublishedFileId_t(Ids[i]));
            if (handle == UGCUpdateHandle_t.Invalid || !SteamUGC.SetItemTags(handle, Tags[i]))
            {{
                ctx.Record(key, "refused before sending");
                continue;
            }}
            string result = null;
            CallResult<SubmitItemUpdateResult_t> call = CallResult<SubmitItemUpdateResult_t>.Create((r, ioFailure) =>
                result = ioFailure ? "IOFailure" : r.m_eResult.ToString().Replace("k_EResult", "")
                    + (r.m_bUserNeedsToAcceptWorkshopLegalAgreement ? " (accept the Workshop legal agreement on Steam)" : ""));
            call.Set(SteamUGC.SubmitItemUpdate(handle, null));
            float deadline = Time.realtimeSinceStartup + {wait}f;
            while (result == null && Time.realtimeSinceStartup < deadline)
            {{
                yield return null;
            }}
            call.Dispose();
            ctx.Record(key, result ?? "no answer from Steam within {wait} s");
        }}
    }}
}}
""".format(ids=ids, tags=tags, wait=STEAM_WAIT)


def probe_project(template):
    """IM-InGameTests' probe template with a reference to the game's Steamworks.NET added."""
    reference = ('  <ItemGroup>\n    <Reference Include="Steamworks.NET">\n'
                 '      <HintPath>$(GameDllDir)\\Steamworks.NET.dll</HintPath>\n'
                 '      <Private>false</Private>\n    </Reference>\n  </ItemGroup>\n')
    if "</Project>" not in template:
        sys.exit("The probe template has no </Project>; has IM-InGameTests changed?")
    return template.replace("</Project>", reference + "</Project>")


def parse_answers(reply):
    """Steam's answer per item id from the probe's 'key = value' reply lines."""
    answers = {}
    for line in reply.splitlines():
        key, sep, value = line.partition(" = ")
        if sep and key.strip().isdigit():
            answers[key.strip()] = value.strip()
    return answers


def write_probe(mods):
    template = os.path.join(INGAMETESTS, "tools", "probe-template", "Probe.csproj")
    with open(template, encoding="utf-8-sig") as f:
        project = probe_project(f.read())
    os.makedirs(PROBE_DIR, exist_ok=True)
    for name in os.listdir(PROBE_DIR):
        if name.endswith((".cs", ".csproj")):
            os.remove(os.path.join(PROBE_DIR, name))
    with open(os.path.join(PROBE_DIR, "WorkshopTags.csproj"), "w", encoding="utf-8", newline="\n") as f:
        f.write(project)
    with open(os.path.join(PROBE_DIR, "WorkshopTags.cs"), "w", encoding="utf-8", newline="\n") as f:
        f.write(probe_source(mods))


def run_probe():
    """Starts a session (main menu, no mods, no save), runs the probe, stops the session. Returns the reply."""
    session = os.path.join(INGAMETESTS, "probe_session.py")
    log_path = os.path.join(PROBE_DIR, "session.log")
    with open(log_path, "w", encoding="utf-8") as log:
        start = subprocess.Popen([sys.executable, session, "start", "--no-save", "--load-vanilla", "--idle", "5", "--max", "15"],
                                 cwd=INGAMETESTS, stdout=log, stderr=subprocess.STDOUT)
        try:
            print("Starting the game on the main menu with no mods (this takes a minute or two) ...", flush=True)
            result = subprocess.run([sys.executable, session, "run", PROBE_DIR, PROBE_METHOD],
                                    cwd=INGAMETESTS, capture_output=True, text=True, errors="replace")
        finally:
            subprocess.run([sys.executable, session, "stop"], cwd=INGAMETESTS, capture_output=True)
            try:
                start.wait(timeout=60)
            except subprocess.TimeoutExpired:
                start.kill()
    if result.returncode != 0:
        with open(log_path, encoding="utf-8", errors="replace") as f:
            started = f.read()
        sys.exit("The probe didn't run:\n{}{}\nSession output:\n{}".format(result.stdout, result.stderr, started))
    return result.stdout


def check_csproj_pushed(paths):
    """Exits unless each csproj is committed and the same as on GitHub. The tags come only from the
    csproj, so the rest of the repo may have unpushed commits."""
    if git("status", "--porcelain", "--", *paths):
        sys.exit("Uncommitted changes in {}; commit and push them first.".format(", ".join(paths)))
    git("fetch", "--quiet", "origin")
    changed = git("diff", "--name-only", "@{u}", "--", *paths)
    if changed:
        sys.exit("Not pushed yet: {}; push it first.".format(changed.replace("\n", ", ")))


def apply(mods, before, dry_run):
    for mod in mods:
        print("{}: set tags to {} (item {})".format(mod.name, show_tags(mod.tags), mod.workshop_id))
    if dry_run:
        print("\n" + probe_source(mods))
        return
    if not os.path.isfile(os.path.join(INGAMETESTS, "probe_session.py")):
        sys.exit("IM-InGameTests not found at {}; set IM_INGAMETESTS to its folder.".format(INGAMETESTS))
    check_csproj_pushed([os.path.relpath(m.csproj, REPO) for m in mods])
    write_probe(mods)
    answers = parse_answers(run_probe())

    after = fetch_details_many([m.workshop_id for m in mods])
    for mod in mods:
        answer = answers.get(mod.workshop_id, "no answer recorded")
        details = after.get(mod.workshop_id)
        remote = steam_tags(details)
        print("{}: Steam answered {}; tags on Steam now: {}".format(mod.name, answer, show_tags(remote)))
        if answer.startswith("AccessDenied"):
            print("  Steam is probably still checking a recent upload of this item; wait, then run --apply once more.")
        if remote is None:
            print("  The item isn't public, so check its tags on the item's page.")
            continue
        old = before.get(mod.workshop_id) or {}
        same = [field for field in ("description", "time_updated") if details.get(field) == old.get(field)]
        print("  Unchanged on Steam: {}.".format(", ".join(same) if same else "nothing checked"))


def main():
    parser = argparse.ArgumentParser(description=(__doc__ or "").partition("\n")[0])
    parser.add_argument("--apply", action="append", default=[], metavar="MOD", help="set this mod's tags on Steam; repeatable")
    parser.add_argument("--apply-all", action="store_true", help="set the tags of every mod whose tags differ")
    parser.add_argument("--dry-run", action="store_true", help="print what would be sent instead of sending it")
    args = parser.parse_args()
    if args.apply and args.apply_all:
        sys.exit("Use --apply or --apply-all, not both.")

    mods = read_mods()
    before = fetch_details_many([m.workshop_id for m in mods])
    statuses = {m.name: compare(m.tags, steam_tags(before.get(m.workshop_id))) for m in mods}
    print_table([(m.name, m.tags, steam_tags(before.get(m.workshop_id)), statuses[m.name]) for m in mods])
    if not args.apply and not args.apply_all:
        return

    print()
    chosen = choose(mods, statuses, args.apply, args.apply_all)
    if not chosen:
        print("Nothing to send.")
        return
    apply(chosen, before, args.dry_run)


if __name__ == "__main__":
    main()
