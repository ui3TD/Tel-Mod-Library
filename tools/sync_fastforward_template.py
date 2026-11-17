"""Compares the IM-FastForward template repo with mods/FastForward, and copies this repo's files over it.

This repo's FastForward is the published mod; the template must hold the same mod. Files fall in three groups:
  - mirrored: the same bytes (CRLF read as LF). The mod's source and assets (source/ in the template), the
    FastForward unit tests, tests/Common/PatchTargetAssert.cs, Mod.BeforeSdk.targets, .editorconfig,
    .gitattributes and NuGet.Config. A file on one side only is a difference.
  - mirrored with known edits: the mod's csproj without the <WorkshopID> line, and the unit tests' csproj with
    mods\\FastForward written as source.
  - same build: Directory.Build.props and tests/Directory.Build.props have the same XML once comments and
    whitespace are removed. Their comments differ; --apply doesn't copy them.

Not compared: the in-game test, the template's tools/ and its README.

Usage:
    python tools/sync_fastforward_template.py [--template <folder>] [--apply]
"""

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_TEMPLATE = os.path.join(os.path.dirname(REPO), "IM-FastForward")

# (folder here, folder in the template): every file below, bin/ and obj/ left out
MIRRORED_TREES = [
    ("mods/FastForward", "source"),
    ("tests/FastForward.Tests", "tests/FastForward.Tests"),
]
MIRRORED_FILES = [
    "tests/Common/PatchTargetAssert.cs",
    "Mod.BeforeSdk.targets",
    ".editorconfig",
    ".gitattributes",
    "NuGet.Config",
]
SAME_BUILD = ["Directory.Build.props", "tests/Directory.Build.props"]
SKIPPED_DIRS = {"bin", "obj"}


def _mod_csproj(text):
    return re.sub(r"[ \t]*<WorkshopID>[^<]*</WorkshopID>\n", "", text)


def _tests_csproj(text):
    return text.replace("mods\\FastForward", "source")


# Mirrored files that get an edit on the way to the template: path here -> edit
EDITED = {
    "mods/FastForward/FastForward.csproj": _mod_csproj,
    "tests/FastForward.Tests/FastForward.Tests.csproj": _tests_csproj,
}


def _files(root, folder):
    """Paths below root/folder, relative to folder, with forward slashes; bin/ and obj/ left out."""
    base = os.path.join(root, folder)
    found = set()
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIPPED_DIRS]
        for name in filenames:
            found.add(os.path.relpath(os.path.join(dirpath, name), base).replace("\\", "/"))
    return found


def _read(path):
    with open(path, "rb") as f:
        return f.read().replace(b"\r\n", b"\n")


def _expected(here, data):
    """The bytes the template's copy of this repo's file must have."""
    if here in EDITED:
        return EDITED[here](data.decode("utf-8")).encode("utf-8")
    return data


def pairs(repo, template):
    """(path here, path in the template, exists here, exists in the template) for every mirrored file."""
    result = []
    for here, there in MIRRORED_TREES:
        ours = _files(repo, here)
        theirs = _files(template, there)
        for name in sorted(ours | theirs):
            result.append((here + "/" + name, there + "/" + name, name in ours, name in theirs))
    for rel in MIRRORED_FILES:
        result.append((rel, rel, os.path.isfile(os.path.join(repo, rel)), os.path.isfile(os.path.join(template, rel))))
    return result


def _build_xml(path):
    """The XML of an MSBuild file with comments and whitespace removed."""
    return ET.canonicalize(from_file=path, with_comments=False, strip_text=True)


def differences(repo, template):
    """One line per difference; empty when the template matches."""
    problems = []
    for here, there, ours, theirs in pairs(repo, template):
        if not ours:
            problems.append("only in the template: " + there)
        elif not theirs:
            problems.append("missing from the template: " + there)
        elif _expected(here, _read(os.path.join(repo, here))) != _read(os.path.join(template, there)):
            problems.append("differs: " + there)
    for rel in SAME_BUILD:
        theirs = os.path.join(template, rel)
        if not os.path.isfile(theirs):
            problems.append("missing from the template: " + rel)
        elif _build_xml(os.path.join(repo, rel)) != _build_xml(theirs):
            problems.append("build differs (comments ignored; fix by hand): " + rel)
    return problems


def apply(repo, template):
    """Copies this repo's mirrored files over the template's and deletes the template's extra ones.
    Returns what changed in the template."""
    changed = []
    for here, there, ours, theirs in pairs(repo, template):
        target = os.path.join(template, there)
        if not ours:
            os.remove(target)
            changed.append("deleted " + there)
            continue
        data = _expected(here, _read(os.path.join(repo, here)))
        if theirs and _read(target) == data:
            continue
        os.makedirs(os.path.dirname(target), exist_ok=True)
        with open(target, "wb") as f:
            f.write(data)
        changed.append("wrote " + there)
    return changed


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--template", default=DEFAULT_TEMPLATE, help="the IM-FastForward folder")
    parser.add_argument("--apply", action="store_true", help="copy this repo's files over the template's")
    args = parser.parse_args()
    if not os.path.isdir(args.template):
        sys.exit("No template at " + args.template)
    if args.apply:
        for line in apply(REPO, args.template):
            print(line)
    problems = differences(REPO, args.template)
    for problem in problems:
        print("FastForward template: " + problem)
    if problems:
        sys.exit(1)
    print("FastForward template matches.")


if __name__ == "__main__":
    main()
