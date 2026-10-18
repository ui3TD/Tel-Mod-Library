"""Record what AI Fan Chatter sends to a real AI service and what comes back, as a Markdown report.

Not a test. It boots the game with the in-game tests' fixture save and runs the `live-report` suite
(tests/InGameTests.TelMods/Reports/AIFanChatterLiveReport.cs). That sends three moments from the save,
a single release, a concert and a graduation, through the mod's own dispatcher, once per model, about
32 seconds apart, so each model's later prompts carry its own earlier posts. Every request costs money
on the key saved in the mod's key file for the chosen service (OpenAI-compatible by default).

    python tools/ai_fan_chatter_live_report.py
    python tools/ai_fan_chatter_live_report.py --models anthropic/claude-haiku-5.5
    python tools/ai_fan_chatter_live_report.py --from-json report.json   # only rewrite the Markdown

Build the mod in Release first. The report goes to ../reports/ai-fan-chatter-live-report.md (outside
the repo), with the raw recording next to it as .json. Its findings section is left for you to write.
"""

import argparse
import datetime
import json
import os
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
RUN = REPO / "tests" / "InGameTests.TelMods" / "run.py"
LOCALLOW = Path(os.environ["USERPROFILE"]) / "AppData" / "LocalLow" / "Glitch Pitch" / "Idol Manager"
REQUEST_FILE = LOCALLOW / "InGameTests" / "aifanchatter-live-report.json"
CSPROJ = REPO / "mods" / "AI Fan Chatter" / "AI Fan Chatter.csproj"
DEFAULT_OUT = REPO.parent / "reports" / "ai-fan-chatter-live-report.md"
MAX_SHOWN = 200
PROVIDERS = {"anthropic": 0, "openai": 1, "compatible": 2}


def record(args) -> Path:
    """Runs the live-report suite and returns the JSON it wrote."""
    raw = args.out.with_suffix(".json")
    raw.parent.mkdir(parents=True, exist_ok=True)
    if raw.exists():
        raw.unlink()
    REQUEST_FILE.parent.mkdir(parents=True, exist_ok=True)
    REQUEST_FILE.write_text(json.dumps({
        "provider": str(PROVIDERS[args.provider]),
        "baseUrl": args.base_url,
        "models": args.models,
        "gapSeconds": str(args.gap),
        "out": str(raw),
    }), encoding="utf-8")
    try:
        cmd = [sys.executable, str(RUN), "--suite", "live-report", "--timeout", str(args.timeout)]
        if args.game_dir:
            cmd += ["--game-dir", str(args.game_dir)]
        code = subprocess.call(cmd)
    finally:
        # Without the request file the suite sends nothing, so no later run can spend money
        REQUEST_FILE.unlink(missing_ok=True)
    if not raw.exists():
        sys.exit(f"The game run (exit code {code}) wrote no recording.")
    return raw


def describe(code: str) -> str:
    hardcore = "devoted" if "_HC_" in code else "casual"
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
        opinion = rest.split("opinion of the group: ", 1)[1].split("/", 1)[0] if "opinion of the group: " in rest else ""
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
        "cost": usage.get("cost", 0) or 0,
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
    sent_schema = any("response_format" in (c.get("request") or "") or "output_config" in (c.get("request") or "") for c in calls[:1])
    if not calls:
        info["schema"] = ""
    elif not sent_schema:
        info["schema"] = "not asked"
    elif any(s in (400, 422) for s in info["statuses"]):
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
        return f"**{name}** ({code}, {describe(code)}, opinion {opinion}/100) · {due_text(due)}"

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
           f"- **Save:** the in-game tests' fixture save, loaded on {data.get('saveDate', '?')}. Game language: {data.get('language') or 'en'}.",
           f"- **Order:** for each model, the moments were sent one after another, about {args.gap} seconds apart (the mod's minimum gap is 30 seconds). Each later prompt therefore carries that model's own earlier posts as feed history, still queued and not yet shown in the feed.",
           "- **Same starting point:** each model started from the save's own feed, so every model got the same first prompt. Their later prompts differ only in the feed history.",
           f"- **Moments:** {', '.join(moments)}, taken from the save.",
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
            out.append(f"| {name} | {m['name']} | {i['input']:,} | {i['output']:,} ({i['reasoning']:,} reasoning) | ${i['cost']:.4f} | {i['seconds']:.1f} | "
                       f"{', '.join(str(s) for s in i['statuses'])} | {i['schema']} | {posts} | {i['cut']} of {i['texts']} |")
    totals = [f"${sum(info[(r['model'], m['name'])]['cost'] for m in r['moments']):.4f} for {name}" for r, name in zip(runs, names)]
    out += ["", "Together the requests cost " + ", ".join(totals) + ".", "",
            "## Findings to investigate", "", "_To be written._", ""]

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
                out += [f"The first reply had only one post, so the mod sent the request again; these are the posts from the last reply.", ""]
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
    ap.add_argument("--gap", type=int, default=32, help="seconds between one model's requests (the mod needs 30)")
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--timeout", type=int, default=1500, help="seconds before the game run is stopped")
    ap.add_argument("--game-dir", type=Path)
    ap.add_argument("--from-json", type=Path, help="render this recording instead of running the game")
    args = ap.parse_args()

    raw = args.from_json or record(args)
    data = json.loads(raw.read_text(encoding="utf-8"), strict=False)
    args.out.write_text(render(data, args), encoding="utf-8")
    print(f"Wrote {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
