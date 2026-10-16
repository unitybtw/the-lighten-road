#!/usr/bin/env python3
"""Render a README-safe animated team dashboard from GitHub-linked commit authors."""

import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from functools import lru_cache
from hashlib import sha256
from io import BytesIO
import json
import math
import os
from pathlib import Path
import re
import subprocess
import urllib.parse
import urllib.request

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "docs/team-activity"
FONT = ROOT / "Game/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
REPO = "unitybtw/the-lighten-road"
TEAM = [
    ("unitybtw", "Siraç", "Game Developer"),
    ("kemal-berke", "Kemal", "Game Developer"),
    ("benesas", "Can", "Artist"),
    ("Ichnolith", "Maksim", "Artist"),
    ("aeshmacodex", "Furkan", "Game Designer"),
]
WIDTH, HEIGHT = 900, 278
SCALE = 3
BACKGROUND = (13, 17, 23)
IVORY = (231, 237, 245)
MUTED = (117, 130, 149)
# Identity colors stay fixed even when commit rankings change.
COLORS = {
    "unitybtw": (84, 153, 255),
    "kemal-berke": (69, 203, 177),
    "benesas": (238, 185, 91),
    "ichnolith": (172, 136, 248),
    "aeshmacodex": (233, 138, 173),
}


def api(path):
    url = "https://api.github.com/" + path
    headers = {"User-Agent": "The-Lighten-Road-Team-Activity", "Accept": "application/vnd.github+json"}
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = "Bearer " + token
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
        return json.load(response), response.headers.get("Link", "")


def commit_counts(revision):
    """Walk the entire immutable default-branch history; count authors, not pushers."""
    counts = Counter()
    unlinked = 0
    path = f"repos/{REPO}/commits?sha={urllib.parse.quote(revision)}&per_page=100"
    while path:
        commits, links = api(path)
        for commit in commits:
            if len(commit.get("parents", [])) > 1:
                continue
            author = commit.get("author")
            if author is None:
                raw = commit["commit"]["author"]
                if "[bot]" not in raw.get("name", "") and "[bot]" not in raw.get("email", ""):
                    unlinked += 1
                continue
            if author.get("type") == "Bot" or author["login"].lower().endswith("[bot]"):
                continue
            counts[author["login"].lower()] += 1
        next_link = re.search(r'<https://api\.github\.com/([^>]+)>; rel="next"', links)
        path = next_link.group(1) if next_link else None
    return counts, unlinked


def member_profile(member):
    login, name, role = member
    profile, _ = api("users/" + login)
    avatar_url = profile["avatar_url"] + "&s=160"
    # The API token is never forwarded to the public avatar host.
    parsed = urllib.parse.urlparse(avatar_url)
    if parsed.scheme != "https" or parsed.hostname != "avatars.githubusercontent.com":
        raise ValueError("Unexpected GitHub avatar host")
    with urllib.request.urlopen(avatar_url, timeout=30) as response:
        avatar = Image.open(BytesIO(response.read())).convert("RGB")
    avatar = ImageOps.fit(avatar, (160, 160), method=Image.Resampling.LANCZOS)
    data = BytesIO()
    avatar.save(data, "PNG", optimize=True)
    return {
        "login": profile["login"], "name": name, "role": role,
        "profile_url": profile["html_url"], "avatar_sha256": sha256(data.getvalue()).hexdigest(),
    }, data.getvalue()


def snapshot(revision, branch):
    counts, unlinked = commit_counts(revision)
    with ThreadPoolExecutor(max_workers=5) as pool:
        profiles = list(pool.map(member_profile, TEAM))
    members = []
    avatar_dir = OUTPUT / "avatars"
    avatar_dir.mkdir(parents=True, exist_ok=True)
    for info, avatar in profiles:
        info["commits"] = counts[info["login"].lower()]
        members.append(info)
        (avatar_dir / (info["login"].lower() + ".png")).write_bytes(avatar)
    team_logins = {m["login"].lower() for m in members}
    data = {
        "repository": REPO, "branch": branch, "members": members,
        "team_commits": sum(m["commits"] for m in members),
        "other_linked_author_commits": sum(v for k, v in counts.items() if k not in team_logins),
        "unlinked_author_commits": unlinked,
        "method": "Non-merge commits authored by GitHub-linked accounts on the default branch; bots excluded. Co-author trailers and work outside Git are not counted.",
    }
    path = OUTPUT / "activity.json"
    previous = json.loads(path.read_text()) if path.exists() else {}
    comparable = {k: v for k, v in previous.items() if k not in ("source_revision", "updated_at")}
    # Ignore bot-only history changes; unchanged data must not create daily empty updates.
    same = comparable == data
    data["source_revision"] = previous["source_revision"] if same else revision
    data["updated_at"] = previous["updated_at"] if same else datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    return data


@lru_cache(maxsize=64)
def font(size):
    return ImageFont.truetype(str(FONT), size * SCALE)


def text(draw, position, value, size, fill=IVORY, anchor=None):
    draw.text(tuple(round(v * SCALE) for v in position), value,
              font=font(size), fill=fill, anchor=anchor)


def bar_segments(members, width):
    """Cumulative rounding preserves the total and gives zero commits zero pixels."""
    total = sum(m["commits"] for m in members)
    accumulated = 0
    for member in members:
        start = round(width * accumulated / total) if total else 0
        accumulated += member["commits"]
        end = round(width * accumulated / total) if total else 0
        yield member, start, end


def render(data):
    # Supersampling keeps small type and circular masks crisp at README sizes.
    members = data["members"]  # Stable roster order also matches the shared bar.
    avatars = {
        m["login"]: Image.open(OUTPUT / "avatars" / (m["login"].lower() + ".png")).convert("RGB")
        for m in members
    }
    bar_x, bar_y, bar_width, bar_height = 90, 230, 720, 12
    segments = list(bar_segments(members, bar_width * SCALE))
    bar_mask = Image.new("L", (bar_width * SCALE, bar_height * SCALE), 0)
    ImageDraw.Draw(bar_mask).rounded_rectangle(
        (0, 0, bar_mask.width - 1, bar_mask.height - 1), radius=bar_mask.height / 2, fill=255)

    def frame_at(phase=None):
        frame = Image.new("RGB", (WIDTH * SCALE, HEIGHT * SCALE), BACKGROUND)
        draw = ImageDraw.Draw(frame)
        text(draw, (38, 23), "TEAM ACTIVITY", 10, MUTED)
        text(draw, (WIDTH - 38, 23), f'{data["team_commits"]} commits', 11, MUTED, "ra")
        pulses = []
        for index in range(len(members)):
            distance = ((phase - index / len(members) + 0.5) % 1 - 0.5) if phase is not None else 0.5
            pulses.append(max(0, math.cos(distance * math.tau)) ** 18)

        # The gently expanding portrait comes to the front of the overlapping row.
        centers = [WIDTH / 2 + (i - (len(members) - 1) / 2) * 76 for i in range(len(members))]
        for index in sorted(range(len(members)), key=lambda i: pulses[i]):
            member, pulse, cx = members[index], pulses[index], centers[index]
            color = COLORS[member["login"].lower()]
            radius, cy = round((46 + 5 * pulse) * SCALE), round((106 - 2 * pulse) * SCALE)
            cx = round(cx * SCALE)
            # A background rim separates overlapping photographs without card chrome.
            rim = radius + 4 * SCALE
            draw.ellipse((cx - rim, cy - rim, cx + rim, cy + rim), fill=BACKGROUND)
            draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=color)
            diameter = 2 * (radius - 2 * SCALE)
            avatar = ImageOps.fit(avatars[member["login"]], (diameter, diameter), method=Image.Resampling.LANCZOS)
            mask = Image.new("L", (diameter, diameter), 0)
            ImageDraw.Draw(mask).ellipse((0, 0, diameter - 1, diameter - 1), fill=255)
            frame.paste(avatar, (cx - diameter // 2, cy - diameter // 2), mask)

        for index, member in enumerate(members):
            color, pulse, cx = COLORS[member["login"].lower()], pulses[index], centers[index]
            name_color = tuple(round(a + pulse * (b - a)) for a, b in zip(IVORY, color))
            text(draw, (cx, 172 - pulse), member["name"], 15, name_color, "ma")
            count = member["commits"]
            text(draw, (cx, 196), f'{count} commit' + ("" if count == 1 else "s"), 10, MUTED, "ma")
            dot = 2 * SCALE
            x, y = round(cx * SCALE), 216 * SCALE
            draw.ellipse((x - dot, y - dot, x + dot, y + dot), fill=color)

        bar = Image.new("RGB", bar_mask.size, (32, 41, 54))
        bar_draw = ImageDraw.Draw(bar)
        for member, start, end in segments:
            if end > start:
                bar_draw.rectangle((start, 0, end - 1, bar.height - 1), fill=COLORS[member["login"].lower()])
        if phase is not None and data["team_commits"]:
            # A soft light passes across the existing fill; proportions never animate.
            center = (phase * 1.4 - 0.2) * bar.width
            for x in range(max(0, round(center - 100 * SCALE)), min(bar.width, round(center + 100 * SCALE))):
                strength = 0.22 * math.exp(-((x - center) / (36 * SCALE)) ** 2)
                original = bar.getpixel((x, 0))
                highlight = tuple(round(v + (255 - v) * strength) for v in original)
                bar_draw.line((x, 0, x, bar.height), fill=highlight)
        frame.paste(bar, (bar_x * SCALE, bar_y * SCALE), bar_mask)
        return frame.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)

    base = frame_at()
    base.save(OUTPUT / "team-activity.png", optimize=True)
    frames = [frame_at(index / 60) for index in range(60)]
    # Include every motion state in one palette, preventing color flicker.
    sheet = Image.new("RGB", (WIDTH, HEIGHT * 7))
    sheet.paste(base, (0, 0))
    for index in range(6):
        sheet.paste(frames[index * 10], (0, (index + 1) * HEIGHT))
    palette = sheet.quantize(colors=256)
    frames = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
    frames[0].save(OUTPUT / "team-activity.gif", save_all=True, append_images=frames[1:],
                   duration=100, loop=0, disposal=1, optimize=True)


def update_readme(data):
    start, end = "<!-- TEAM-ACTIVITY:START -->", "<!-- TEAM-ACTIVITY:END -->"
    rows = []
    for _, member in sorted(enumerate(data["members"]), key=lambda item: (-item[1]["commits"], item[0])):
        rows.append(f'| [{member["name"]}]({member["profile_url"]}) | {member["role"]} | {member["commits"]} |')
    section = f'''{start}
<a href="https://github.com/{REPO}/commits/{data['branch']}/">
  <picture>
    <source media="(prefers-reduced-motion: reduce)" srcset="docs/team-activity/team-activity.png">
    <img src="docs/team-activity/team-activity.gif" alt="Team activity: overlapping circular GitHub portraits with names and commit counts above one color-coded proportional bar. Siraç is blue." width="900">
  </picture>
</a>

<details>
<summary>Commit counts and methodology</summary>

| Team member | Role | Authored commits |
| --- | --- | ---: |
{chr(10).join(rows)}

Counts cover GitHub-linked commit authors on `{data['branch']}`, excluding merge commits and bots. Co-author trailers, issues, reviews, uncommitted assets and work outside Git are not counted. Commit volume is an activity signal, not a measure of effort or work quality.

[View the data](docs/team-activity/activity.json) · [Static image](docs/team-activity/team-activity.png) · [How updates work](docs/team-activity/README.md)

</details>
{end}'''
    path = ROOT / "README.md"
    content = path.read_text()
    if start not in content or end not in content:
        raise ValueError("README team activity markers are missing")
    before, remainder = content.split(start, 1)
    _, after = remainder.split(end, 1)
    path.write_text(before + section + after)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--from-snapshot", action="store_true", help="Render existing data without network calls")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if args.from_snapshot:
        data = json.loads((OUTPUT / "activity.json").read_text())
    else:
        repo, _ = api("repos/" + REPO)
        branch = repo["default_branch"]
        revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        data = snapshot(revision, branch)
    render(data)
    update_readme(data)
    print(json.dumps({m["login"]: m["commits"] for m in data["members"]}))


if __name__ == "__main__":
    main()
