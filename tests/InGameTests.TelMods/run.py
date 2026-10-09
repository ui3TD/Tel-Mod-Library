"""Run the in-game tests with this repo's Tel mod checks (InGameTests.TelMods) loaded.

Builds the checks, which deploys them next to the runner plugin in the game, then runs
IM-InGameTests' run_ingame_tests.py. Every argument except --build-mods is passed to the runner.

    python tests/InGameTests.TelMods/run.py              # smoke suite, with the Tel mod checks
    python tests/InGameTests.TelMods/run.py --weeks 12 -v
    python tests/InGameTests.TelMods/run.py --only "Fan Attrition"
    python tests/InGameTests.TelMods/run.py --build-mods   # build and deploy the installed Tel mods first

The runner's per-mod checks are limited to the Tel mods with --scope-file scope.txt; other
installed mods stay loaded and are noted, not checked.

Without --build-mods the mods are not built: build a changed mod in Release first, which deploys
it. The LoadedBuildsMatchCheckout check fails when a loaded mod isn't this checkout's last build,
but it can't see edits made since that build.
Needs IM-InGameTests next to this repo, or its folder in the IM_INGAMETESTS environment variable.
"""

import argparse
import importlib.util
import os
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
RUNNER = Path(os.environ.get("IM_INGAMETESTS", HERE.parents[2] / "IM-InGameTests")) / "run_ingame_tests.py"
LOCAL_MODS = Path(os.environ["USERPROFILE"]) / "AppData" / "LocalLow" / "Glitch Pitch" / "Idol Manager" / "Mods"


def load_runner():
    """The runner script as a module, for its game-folder lookup."""
    spec = importlib.util.spec_from_file_location("run_ingame_tests", RUNNER)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def installed_mod_projects() -> list[Path]:
    """Mod projects with a local copy in the game's Mods folder (a Release build deploys there)."""
    projects = []
    for project in sorted((REPO / "mods").glob("*/*.csproj")):
        match = re.search(r"<ModName>([^<]+)</ModName>", project.read_text(encoding="utf-8-sig"))
        if match and (LOCAL_MODS / match.group(1).strip()).is_dir():
            projects.append(project)
    return projects


def build(project: Path, *props: str) -> bool:
    print(f"Building {project.name} ...", flush=True)
    result = subprocess.run(["dotnet", "build", str(project), "-c", "Release", "-nologo", "-v", "q", *props],
                            capture_output=True, text=True)
    if result.returncode != 0:
        print(result.stdout[-4000:], result.stderr[-2000:])
    return result.returncode == 0


def main() -> int:
    ap = argparse.ArgumentParser(add_help=False)
    ap.add_argument("--build-mods", action="store_true")
    ap.add_argument("--game-dir", type=Path)
    ap.add_argument("-h", "--help", action="store_true")
    args, _ = ap.parse_known_args()
    runner_args = [a for a in sys.argv[1:] if a != "--build-mods"]

    if not RUNNER.is_file():
        print(f"IM-InGameTests not found at {RUNNER.parent}; set IM_INGAMETESTS to its folder.")
        return 2
    if args.help:
        print(__doc__, flush=True)
        return subprocess.call([sys.executable, str(RUNNER), "--help"])

    game_dir = args.game_dir or load_runner().find_game_dir()
    if game_dir is None:
        print("Idol Manager not found; pass --game-dir.")
        return 2

    projects = (installed_mod_projects() if args.build_mods else []) + [HERE / "InGameTests.TelMods.csproj"]
    for project in projects:
        if not build(project, f"-p:GameDir={game_dir}"):
            return 2

    return subprocess.call([sys.executable, str(RUNNER), "--scope-file", str(HERE / "scope.txt"), *runner_args])


if __name__ == "__main__":
    sys.exit(main())
