#!/usr/bin/env python3
"""Render a README-safe animated team dashboard from GitHub-linked commit authors."""

import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
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
WIDTH, HEIGHT = 1200, 570
GOLD = (231, 182, 109)
IVORY = (242, 232, 212)
MUTED = (139, 153, 166)


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


def font(size):
    return ImageFont.truetype(str(FONT), size)


def text(draw, position, value, size, fill=IVORY, anchor=None):
    draw.text(position, value, font=font(size), fill=fill, anchor=anchor)


def circle_avatar(frame, login, center):
    avatar = Image.open(OUTPUT / "avatars" / (login.lower() + ".png")).convert("RGB")
    avatar = ImageOps.fit(avatar, (76, 76), method=Image.Resampling.LANCZOS)
    mask = Image.new("L", (304, 304), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, 303, 303), fill=255)
    mask = mask.resize((76, 76), Image.Resampling.LANCZOS)
    frame.paste(avatar, (center[0] - 38, center[1] - 38), mask)


def render(data):
    base = Image.new("RGB", (WIDTH, HEIGHT))
    draw = ImageDraw.Draw(base)
    for y in range(HEIGHT):
        t = y / HEIGHT
        draw.line((0, y, WIDTH, y), fill=(int(12 + t * 2), int(18 + t * 2), int(25 + t * 3)))
    draw.rounded_rectangle((0, 0, WIDTH - 1, HEIGHT - 1), radius=18, outline=(41, 51, 60), width=1)
    draw.line((26, 0, 290, 0), fill=GOLD, width=2)
    text(draw, (28, 27), "THE PEOPLE BEHIND THE LIGHT", 11, GOLD)
    text(draw, (27, 51), "Building the road, together.", 34)
    text(draw, (29, 103), "Repository activity  /  five teammates, one shared world", 14, MUTED)
    text(draw, (1168, 39), str(data["team_commits"]), 43, IVORY, "ra")
    text(draw, (1168, 92), "TEAM COMMITS", 10, MUTED, "ra")
    members = sorted(enumerate(data["members"]), key=lambda item: (-item[1]["commits"], item[0]))
    maximum = max((m["commits"] for m in data["members"]), default=0)
    cards = []
    for column, (_, member) in enumerate(members):
        x, y, w = 25 + column * 233, 155, 218
        leading = maximum > 0 and member["commits"] == maximum
        border = (116, 90, 56) if leading else (44, 56, 65)
        fill = (28, 28, 27) if leading else (18, 26, 34)
        draw.rounded_rectangle((x, y, x + w, 480), radius=12, fill=fill, outline=border)
        text(draw, (x + 18, 173), "MOST COMMITS" if leading else "TEAM CONTRIBUTOR", 9, GOLD if leading else MUTED)
        center = (x + w // 2, 239)
        draw.ellipse((center[0] - 42, center[1] - 42, center[0] + 42, center[1] + 42), outline=border, width=2)
        circle_avatar(base, member["login"], center)
        text(draw, (center[0], 291), member["name"], 23, IVORY, "ma")
        text(draw, (center[0], 324), "@" + member["login"], 13, MUTED, "ma")
        text(draw, (center[0], 347), member["role"], 12, MUTED, "ma")
        text(draw, (x + 18, 388), str(member["commits"]), 33, GOLD if leading else IVORY)
        count_width = draw.textlength(str(member["commits"]), font=font(33))
        text(draw, (x + 23 + count_width, 408), "COMMITS", 9, MUTED)
        share = member["commits"] / max(1, data["team_commits"])
        text(draw, (x + w - 18, 408), f"{share:.0%}", 11, MUTED, "ra")
        draw.rounded_rectangle((x + 18, 448, x + w - 18, 452), radius=2, fill=(45, 54, 60))
        length = (w - 36) * share
        if length > 0:
            draw.rounded_rectangle((x + 18, 448, x + 18 + max(3, length), 452), radius=2, fill=GOLD if leading else (120, 153, 163))
        cards.append((center, leading, x + 18, length))
    draw.line((28, 502, 1172, 502), fill=(42, 52, 61))
    text(draw, (29, 517), "Commits show activity, not effort. Art, design, reviews and work outside Git also matter.", 13, MUTED)
    text(draw, (29, 543), data["branch"] + "  /  author-linked commits  /  merges & bots excluded", 10, MUTED)
    text(draw, (1170, 543), "Updated " + data["updated_at"], 10, MUTED, "ra")
    base.save(OUTPUT / "team-activity.png", optimize=True)

    frames = []
    # Figures and bar lengths remain accurate in every frame; motion is purely decorative.
    for index in range(48):
        phase = index / 48
        frame = base.copy()
        draw = ImageDraw.Draw(frame)
        for column, (center, leading, start, length) in enumerate(cards):
            if leading:
                pulse = (math.sin(phase * math.tau) + 1) / 2
                ring = tuple(int(a + pulse * (b - a)) for a, b in zip((103, 81, 52), GOLD))
                draw.ellipse((center[0] - 42, center[1] - 42, center[0] + 42, center[1] + 42), outline=ring, width=2)
            if length >= 4:
                shimmer = (phase + column * 0.13) % 1
                tip = start + min(length, max(3, length)) * shimmer
                draw.line((tip, 449, min(start + max(3, length), tip + 10), 449), fill=IVORY, width=1)
        frames.append(frame)
    # A single shared palette avoids avatar / text color flicker between GIF frames.
    palette = base.quantize(colors=192)
    frames = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
    frames[0].save(OUTPUT / "team-activity.gif", save_all=True, append_images=frames[1:],
                   duration=125, loop=0, disposal=1, optimize=True)


def update_readme(data):
    start, end = "<!-- TEAM-ACTIVITY:START -->", "<!-- TEAM-ACTIVITY:END -->"
    rows = []
    for _, member in sorted(enumerate(data["members"]), key=lambda item: (-item[1]["commits"], item[0])):
        rows.append(f'| [{member["name"]}]({member["profile_url"]}) | {member["role"]} | {member["commits"]} |')
    section = f'''{start}
<a href="https://github.com/{REPO}/commits/{data['branch']}/">
  <picture>
    <source media="(prefers-reduced-motion: reduce)" srcset="docs/team-activity/team-activity.png">
    <img src="docs/team-activity/team-activity.gif" alt="Team commit activity: circular GitHub profile photos, commit counts and each member's share of team commits" width="1200">
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
