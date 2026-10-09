"""Run the in-game tests with this repo's Tel mod checks (InGameTests.TelMods) loaded.

Calls IM-InGameTests' run_ingame_tests.py with --build for this project, so every run
rebuilds and deploys the checks. Every other argument is passed through to the runner.

    python tests/InGameTests.TelMods/run.py              # smoke suite, with the Tel mod checks
    python tests/InGameTests.TelMods/run.py --weeks 12 -v
    python tests/InGameTests.TelMods/run.py --only "Fan Attrition"
    python tests/InGameTests.TelMods/run.py --build-mods   # build and deploy the installed Tel mods first

Without --build-mods the mods are not built: build a changed mod in Release first, which deploys
it. The LoadedBuildsMatchCheckout check fails when a loaded mod isn't this checkout's last build,
but it can't see edits made since that build.
Needs IM-InGameTests next to this repo, or its folder in the IM_INGAMETESTS environment variable.
"""

import os
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
RUNNER = Path(os.environ.get("IM_INGAMETESTS", HERE.parents[2] / "IM-InGameTests")) / "run_ingame_tests.py"
LOCAL_MODS = Path(os.environ["USERPROFILE"]) / "AppData" / "LocalLow" / "Glitch Pitch" / "Idol Manager" / "Mods"


def installed_mod_projects() -> list[Path]:
    """Mod projects with a local copy in the game's Mods folder (a Release build deploys there)."""
    projects = []
    for project in sorted((REPO / "mods").glob("*/*.csproj")):
        match = re.search(r"<ModName>([^<]+)</ModName>", project.read_text(encoding="utf-8-sig"))
        if match and (LOCAL_MODS / match.group(1).strip()).is_dir():
            projects.append(project)
    return projects


def main() -> int:
    args = sys.argv[1:]
    if not RUNNER.is_file():
        print(f"IM-InGameTests not found at {RUNNER.parent}; set IM_INGAMETESTS to its folder.")
        return 2
    if "-h" in args or "--help" in args:
        print(__doc__, flush=True)

    builds = [HERE / "InGameTests.TelMods.csproj"]
    if "--build-mods" in args:
        args.remove("--build-mods")
        builds += installed_mod_projects()

    cmd = [sys.executable, str(RUNNER), *args]
    for project in builds:
        cmd += ["--build", str(project)]
    return subprocess.call(cmd)


if __name__ == "__main__":
    sys.exit(main())
