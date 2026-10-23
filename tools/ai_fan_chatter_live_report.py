"""Record what AI Fan Chatter sends to a real AI service and what comes back, as a Markdown report.

Not a test, and it doesn't start the game. It builds and runs tools/AIFanChatterLiveReport, which
fills the game's lists with a made-up agency (Agency.cs there) and sends twenty of its moments, a single
release, a concert, one of the game's random events, a dating scandal, a paid policy change, a staff firing, a tour, an election's results, a concert with its election's results, a cancelled election, the awards show, a show starting, a long-running show cancelled, a business contract starting, new members from an audition, an idol going on hiatus, an idol back from hiatus, another mod's event, a graduation announcement and a graduation, through the mod's own prompt, dispatcher and reply code, once per
model, one after another, so each model's later prompts carry its own earlier posts. It reads the
game's English text from the install folder. Every request costs money on the key saved in the mod's
key file for the chosen service (OpenAI-compatible by default).

    python tools/ai_fan_chatter_live_report.py
    python tools/ai_fan_chatter_live_report.py --models anthropic/claude-haiku-5.5
    python tools/ai_fan_chatter_live_report.py --dry-run                 # print the prompts; sends nothing
    python tools/ai_fan_chatter_live_report.py --moments concert --setlist "Signal Fire/Rin; talk: Mao, Kaede"
    python tools/ai_fan_chatter_live_report.py --moments event --event stream_rant --event-idols Mao
    python tools/ai_fan_chatter_live_report.py --from-json report.json   # only rewrite the Markdown

The report goes to ../reports/ai-fan-chatter/ai-fan-chatter-live-report.md (outside the repo), with the raw recording
next to it as .json. Its findings section is left for you to write.
"""

import argparse
import datetime
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
RUN = REPO / "tests" / "InGameTests.TelMods" / "run.py"
TOOL = REPO / "tools" / "AIFanChatterLiveReport" / "AIFanChatterLiveReport.csproj"
CSPROJ = REPO / "mods" / "AI Fan Chatter" / "AI Fan Chatter.csproj"
DEFAULT_OUT = REPO.parent / "reports" / "ai-fan-chatter" / "ai-fan-chatter-live-report.md"
MAX_SHOWN = 200
PROVIDERS = {"anthropic": 0, "openai": 1, "compatible": 2}


def find_game_dir() -> Path:
    """The game's install folder, found as the in-game tests find it."""
    spec = importlib.util.spec_from_file_location("telmods_run", RUN)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    game_dir = module.load_runner().find_game_dir()
    if game_dir is None:
        sys.exit("Idol Manager not found; pass --game-dir.")
    return game_dir


def run_tool(args, *options: str) -> None:
    """Builds and runs tools/AIFanChatterLiveReport with these options."""
    game_dir = args.game_dir or find_game_dir()
    cmd = ["dotnet", "run", "--project", str(TOOL), "--", "--game-dir", str(game_dir), *options]
    code = subprocess.call(cmd)
    if code != 0:
        sys.exit(f"tools/AIFanChatterLiveReport failed (exit code {code}).")


def scenario(args) -> list:
    """The tool's options for which moments to send and the concert's set list."""
    options = []
    for moment in args.moments or []:
        options += ["--moment", moment]
    for name in ("setlist", "event", "event_reply", "event_idols"):
        value = getattr(args, name)
        if value is not None:
            options += ["--" + name.replace("_", "-"), str(value)]
    if args.firing_budget:
        options += ["--firing-budget", "1"]
    if args.firing_role:
        options += ["--firing-role", args.firing_role]
    if args.single:
        options += ["--single", args.single]
    if args.graduation_successor:
        options += ["--graduation-successor", args.graduation_successor]
    if args.graduation_no_staff:
        options += ["--graduation-staff", "0"]
    if args.graduation_notice:
        options += ["--graduation-notice", args.graduation_notice]
    if args.hiatus_reason:
        options += ["--hiatus-reason", args.hiatus_reason]
    return options


def record(args) -> Path:
    """Runs the report tool and returns the JSON it wrote."""
    raw = args.out.with_suffix(".json")
    raw.parent.mkdir(parents=True, exist_ok=True)
    if raw.exists():
        raw.unlink()
    options = ["--provider", str(PROVIDERS[args.provider]), "--base-url", args.base_url, "--out", str(raw)]
    for model in args.models:
        options += ["--model", model]
    run_tool(args, *options, *scenario(args))
    if not raw.exists():
        sys.exit("The report tool wrote no recording.")
    return raw


def describe(code: str) -> str:
    hardcore = "hardcore" if "_HC_" in code else "casual"
    age = "teen" if code.endswith("_T") else "young adult" if code.endswith("_YA") else "adult"
    gender = ("boy" if age == "teen" else "man") if code.startswith("M") else ("girl" if age == "teen" else "woman")
    return f"{hardcore} {age} {gender}"


def fans_from(system: str) -> dict:
    """Each fan code's handle and opinion, from the fan accounts in the system prompt."""
    fans = {}
    for line in system.splitlines():
        if not line.startswith("- ") or ":" not in line:
            continue
        code, rest = line[2:].split(":", 1)
        if "_" not in code or " " in code:
            continue
        handle = rest.split("(shown as @", 1)[1].split(")", 1)[0] if "(shown as @" in rest else ""
        opinion = rest.split("opinion of the group: ", 1)[1].split("%", 1)[0].split("/", 1)[0] if "opinion of the group: " in rest else ""
        fans[code] = (handle, opinion)
    return fans


def prompts(body: dict) -> tuple[str, str]:
    system = body.get("system", "")
    user = ""
    for message in body.get("messages", []):
        if message.get("role") == "system":
            system = message.get("content", "")
        elif message.get("role") == "user":
            user = message.get("content", "")
    return system, user


def reply_text(response: dict) -> str:
    if "choices" in response:
        return (response["choices"][0].get("message") or {}).get("content") or ""
    return "".join(part.get("text", "") for part in response.get("content", []) if part.get("type") == "text")


def usage_of(response: dict) -> dict:
    usage = response.get("usage") or {}
    return {
        "input": usage.get("prompt_tokens", usage.get("input_tokens", 0)),
        "output": usage.get("completion_tokens", usage.get("output_tokens", 0)),
        "reasoning": (usage.get("completion_tokens_details") or {}).get("reasoning_tokens", 0),
        # With the player's own key on OpenRouter (BYOK), "cost" is 0 and the provider's charge is upstream
        "cost": usage.get("cost", 0) or (usage.get("cost_details") or {}).get("upstream_inference_cost", 0) or 0,
    }


def loads(text: str):
    try:
        return json.loads(text, strict=False)
    except (ValueError, TypeError):
        return None


def strip_feed(text: str) -> str:
    return text.removeprefix("<noparse>").removesuffix("</noparse>")


def due_text(due: str) -> str:
    return datetime.datetime.strptime(due, "%Y-%m-%d %H:%M").strftime("%d %b %H:%M").lstrip("0")


def model_name(model: str) -> str:
    """anthropic/claude-haiku-5.5 -> Haiku 5.5"""
    name = model.split("/")[-1].removeprefix("claude-")
    parts = name.split("-", 1)
    return parts[0].capitalize() + (" " + parts[1].replace("-", ".") if len(parts) > 1 else "")


def analyse(moment: dict) -> dict:
    """Totals and the parsed reply for one moment of one model."""
    calls = moment.get("calls", [])
    info = {"input": 0, "output": 0, "reasoning": 0, "cost": 0.0, "seconds": 0.0, "statuses": [], "schema": "",
            "replies": [], "usages": [], "cut": 0, "texts": 0, "requests": 0, "user": "", "system": "", "providers": []}
    for i, call in enumerate(calls):
        body = loads(call.get("request", "")) or {}
        response = loads(call.get("response", "")) or {}
        # Google wraps an error in a list: [{"error": {...}}]
        if isinstance(response, list):
            response = response[0] if response and isinstance(response[0], dict) else {}
        if i == 0:
            info["system"], info["user"] = prompts(body)
        info["requests"] += 1
        status = int(float(call.get("status", "0") or 0))
        info["statuses"].append(status)
        if "repliedAt" in call:
            info["seconds"] += float(call["repliedAt"]) - float(call["sentAt"])
        usage = usage_of(response)
        for key in ("input", "output", "reasoning", "cost"):
            info[key] += usage[key]
        if response.get("provider"):
            info["providers"].append(response["provider"])
        text = reply_text(response)
        info["replies"].append(text)
        info["usages"].append(response.get("usage") or {})
        parsed = loads(text[text.find("{"):text.rfind("}") + 1]) if "{" in text else None
        if isinstance(parsed, dict):
            for post in parsed.get("posts", []):
                for item in [post] + list(post.get("replies") or []):
                    info["texts"] += 1
                    info["cut"] += len(item.get("text") or "") > MAX_SHOWN
    def has_schema(call: dict) -> bool:
        request = call.get("request") or ""
        return "response_format" in request or '"format"' in request

    # A 400 can be about the effort setting only, so the schema counts as rejected only when the last
    # request went without it
    if not calls:
        info["schema"] = ""
    elif not has_schema(calls[0]):
        info["schema"] = "not asked"
    elif not has_schema(calls[-1]):
        info["schema"] = "rejected, sent without"
    else:
        info["schema"] = "accepted"
    queued = moment.get("queued", [])
    info["posts"] = sum(1 for q in queued if q["isReply"] != "true")
    info["reply_count"] = sum(1 for q in queued if q["isReply"] == "true")
    info["retried"] = info["requests"] > 1 and all(s == 200 for s in info["statuses"])
    return info


def posts_markdown(queued: list, fans: dict, raw_texts: list) -> str:
    def who(code: str, due: str) -> str:
        handle, opinion = fans.get(code, ("", ""))
        name = f"@{handle}" if handle else code
        return f"**{name}** ({code}, {describe(code)}, opinion {opinion}%) · {due_text(due)}"

    def quote(text: str) -> str:
        shown = strip_feed(text)
        cut = shown.endswith("...") and any(len(t) > MAX_SHOWN and t.startswith(shown[:-3].rstrip()) for t in raw_texts)
        return "> " + shown + (f" *(cut at {MAX_SHOWN} characters)*" if cut else "")

    posts = [q for q in queued if q["isReply"] != "true"]
    replies = [q for q in queued if q["isReply"] == "true"]
    lines = []
    for n, post in enumerate(posts, 1):
        lines += [f"{n}. {who(post['user'], post['due'])}", "", "   " + quote(post["text"]), ""]
        for reply in replies:
            if reply["parentUser"] == post["user"] and reply["parentText"] == post["text"]:
                lines += [f"   - ↳ {who(reply['user'], reply['due'])}", "", "     " + quote(reply["text"]), ""]
    return "\n".join(lines).rstrip() if lines else "No posts."


def raw_texts_of(replies: list) -> list:
    texts = []
    for text in replies:
        parsed = loads(text[text.find("{"):text.rfind("}") + 1]) if "{" in text else None
        if isinstance(parsed, dict):
            for post in parsed.get("posts", []):
                for item in [post] + list(post.get("replies") or []):
                    texts.append(item.get("text") or "")
    return texts


def pretty(text: str) -> str:
    parsed = loads(text)
    return json.dumps(parsed, ensure_ascii=False, indent=2) if parsed is not None else text


def render(data: dict, args) -> str:
    runs = data["runs"]
    names = [model_name(r["model"]) for r in runs]
    version = next((line.split(">")[1].split("<")[0] for line in CSPROJ.read_text(encoding="utf-8-sig").splitlines() if "<Version>" in line), "?")
    moments = [m["name"] for m in runs[0]["moments"]] if runs else []
    info = {(r["model"], m["name"]): analyse(m) for r in runs for m in r["moments"]}
    today = datetime.date.today()

    out = [f"# AI Fan Chatter: live prompt and output report, {' and '.join(names)}", "",
           f"Recorded on {today.day} {today:%B %Y} from the current build: {version}. The same {len(moments)} moments were sent to "
           f"{'each of ' if len(runs) > 2 else ''}{len(runs)} model{'s' if len(runs) != 1 else ''}. Each section below shows the exact prompt the mod sent and the posts that came back, as the game would show them.",
           "", "## Setup", "",
           f"- **Service and models:** {args.base_url} with {', '.join('`' + r['model'] + '`' for r in runs)}, using the player's saved key. The mod's settings were otherwise its defaults.",
           f"- **Game state:** the made-up agency in tools/AIFanChatterLiveReport/Agency.cs, on {data.get('gameDate', '?')}, with the game's {data.get('language') or 'en'} text. The game wasn't running.",
           "- **Order:** for each model, the moments were sent one after another, with the mod's 30 seconds between requests skipped. Each later prompt therefore carries that model's own earlier posts as feed history, still queued and not yet shown in the feed.",
           "- **Same starting point:** each model started from the same agency and feed, so every model got the same first prompt. Their later prompts differ only in the feed history.",
           f"- **Moments:** {', '.join(moments)}, taken from the agency.",
           "", "## Summary", "",
           f"| Model | Moment | Input tokens | Output tokens | Cost | Seconds | HTTP | Reply schema | Posts | Texts cut at {MAX_SHOWN} characters |",
           "|---|---|---|---|---|---|---|---|---|---|"]
    for r, name in zip(runs, names):
        for m in r["moments"]:
            i = info[(r["model"], m["name"])]
            if m.get("error"):
                out.append(f"| {name} | {m['name']} | | | | | | | {m['error']} | |")
                continue
            posts = f"{i['posts']} post{'s' if i['posts'] != 1 else ''}, {i['reply_count']} repl{'ies' if i['reply_count'] != 1 else 'y'}"
            if i["retried"]:
                posts += f" (after a retry: {i['requests']} requests)"
            if m.get("stopped") == "true":
                posts += " (requests stopped)"
            out.append(f"| {name} | {m['name']} | {i['input']:,} | {i['output']:,} ({i['reasoning']:,} reasoning) | \\${i['cost']:.4f} | {i['seconds']:.1f} | "
                       f"{', '.join(str(s) for s in i['statuses'])} | {i['schema']} | {posts} | {i['cut']} of {i['texts']} |")
    # A bare $ pair renders as math in Markdown previews
    totals = [f"\\${sum(info[(r['model'], m['name'])]['cost'] for m in r['moments']):.4f} for {name}" for r, name in zip(runs, names)]
    out += ["", "Together the requests cost " + ", ".join(totals) + "."]
    # OpenRouter passes requests on to the player's own provider key (BYOK) when one is set up there
    byok = sorted({p for i in info.values() if any(u.get("is_byok") for u in i["usages"]) for p in i["providers"]})
    if byok:
        out += ["", f"**Billed to your {' and '.join(byok)} account, not OpenRouter:** OpenRouter sent these requests "
                    "with your own provider key, so the costs above are that provider's charges."]
    out += ["", "## Findings to investigate", "", "_To be written._", ""]

    systems = {i["system"] for i in info.values() if i["system"]}
    first_system = next((i["system"] for i in info.values() if i["system"]), "")
    out += ["## The system prompt", "",
            "The system prompt was identical for every request." if len(systems) == 1 else "The system prompt differed between requests; this is the first one.",
            "", "```text", first_system, "```", ""]
    fans = fans_from(first_system)

    for n, moment in enumerate(moments, 1):
        out += [f"## Moment {n}: {moment}", "", "### The user message", ""]
        users = [(name, info[(r["model"], moment)]["user"]) for r, name in zip(runs, names)]
        if len({u for _, u in users}) == 1:
            out += [("Both models" if len(users) == 2 else "Every model") + " got this same message." if len(users) > 1 else "", "",
                    "```text", users[0][1], "```", ""]
        else:
            out += [f"This is the message {users[0][0]} got. The others differ only in the feed history, which holds each model's own earlier posts; they're in the collapsed sections below.",
                    "", "```text", users[0][1], "```", ""]
            for name, user in users[1:]:
                out += [f"<details><summary>The message {name} got</summary>", "", "```text", user, "```", "", "</details>", ""]
        for r, name in zip(runs, names):
            m = next(x for x in r["moments"] if x["name"] == moment)
            i = info[(r["model"], moment)]
            out += [f"### {name}'s posts", ""]
            if m.get("error"):
                out += [m["error"], ""]
                continue
            if i["retried"]:
                out += ["The first reply had only one post, so the mod sent the request again; these are the posts from the last reply.", ""]
            if m.get("notice"):
                out += [f"The mod's notice: \"{m['notice']}\"", ""]
            if m.get("stopped") == "true":
                out += ["The mod stopped requests here until the player presses Apply. The report carried on with the next moment as if the player had.", ""]
            out += [posts_markdown(m.get("queued", []), fans, raw_texts_of(i["replies"])), "",
                    f"<details><summary>{name}'s raw repl{'ies' if len(i['replies']) > 1 else 'y'} and usage</summary>", ""]
            for k, (reply, usage) in enumerate(zip(i["replies"], i["usages"]), 1):
                if len(i["replies"]) > 1:
                    out += [f"Request {k}:", ""]
                out += ["```json", pretty(reply), "```", "", "```json", json.dumps(usage, ensure_ascii=False, indent=2), "```", ""]
            out += ["</details>", ""]
    return "\n".join(out).rstrip() + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--models", nargs="+", default=["anthropic/claude-haiku-5.5", "anthropic/claude-opus-5.5"])
    ap.add_argument("--provider", choices=PROVIDERS, default="compatible")
    ap.add_argument("--base-url", default="https://openrouter.ai/api/v1")
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--game-dir", type=Path, help="the game's install folder, for its text (found through Steam if left out)")
    ap.add_argument("--dry-run", action="store_true", help="print the prompts the first model would get; sends nothing")
    ap.add_argument("--moments", nargs="+", choices=["single", "concert", "event", "scandal", "policy", "firing", "tour", "election", "concert-election", "election-cancel", "awards", "show", "show-end", "contract", "audition", "hiatus", "hiatus-end", "mod-event", "graduation-announcement", "graduation", "chatter", "chatter-2", "chatter-3"], help="the moments to send, in their usual order (all twenty if left out)")
    ap.add_argument("--event", help="the random event's id in the game's random_events.json (default onions_30)")
    ap.add_argument("--event-reply", type=int, help="the player's answer to the event, from 0 (default: the first one without posts of its own)")
    ap.add_argument("--event-idols", help="who plays the event's idols, by first name, split by commas (default: Rin)")
    ap.add_argument("--hiatus-reason", choices=["injury", "health", "none"], help="the hiatus's reason: an injury, health reasons (depression, never named) or none given (default: health)")
    ap.add_argument("--single", choices=["plain", "voting", "election"], help="the single as a plain release (default), an upcoming election's voting single, or the single made from Election #1's results")
    ap.add_argument("--firing-role", choices=["choreographer", "dance_coach", "sales_manager"], help="the fired staffer's role (default: choreographer, production staff)")
    ap.add_argument("--graduation-successor", choices=["sister", "successor", "none"], help="who the graduate brings in from her goodbye scene (default: sister)")
    ap.add_argument("--graduation-no-staff", action="store_true", help="the graduate doesn't stay at the agency as staff (by default she does, as a choreographer)")
    ap.add_argument("--graduation-notice", choices=["announced", "fired", "sudden"], help="whether the graduate announced it 3 months before, was fired by the producer, or left at once (default: announced); only an announced graduation has a successor or staff job")
    ap.add_argument("--firing-budget", action="store_true", help="the fired staffer's deal was accepted: budget problems are announced instead of blame")
    ap.add_argument("--setlist", help='the concert\'s set list, items split by ";": "Single title/center" or "talk: idol, idol", idols by first name')
    ap.add_argument("--from-json", type=Path, help="render this recording instead of sending requests")
    args = ap.parse_args()

    if args.dry_run:
        run_tool(args, "--dry-run", "1", *scenario(args))
        return 0

    raw = args.from_json or record(args)
    data = json.loads(raw.read_text(encoding="utf-8"), strict=False)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(render(data, args), encoding="utf-8")
    print(f"Wrote {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
