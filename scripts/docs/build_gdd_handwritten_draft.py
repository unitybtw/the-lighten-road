"""Build a compact 4-page GDD blueprint designed to be handwritten cleanly in a 2-3 hour class session."""
import math
from pathlib import Path
from shutil import copy2
from reportlab.graphics.shapes import Circle, Drawing, Line, Polygon, Rect, String
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    CondPageBreak,
    KeepTogether,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output/pdf/The_Lighten_Road_GDD_Handwritten_Guide.pdf"
DESKTOP_OUT = Path.home() / "Desktop/The_Lighten_Road_GDD_Handwritten_Guide.pdf"
FONT_DIR = Path("/System/Library/Fonts/Supplemental")

pdfmetrics.registerFont(TTFont("DisplayBold", str(FONT_DIR / "Georgia Bold.ttf")))
pdfmetrics.registerFont(TTFont("Body", str(FONT_DIR / "Arial.ttf")))
pdfmetrics.registerFont(TTFont("BodyBold", str(FONT_DIR / "Arial Bold.ttf")))
pdfmetrics.registerFont(TTFont("BodyItalic", str(FONT_DIR / "Arial Italic.ttf")))
pdfmetrics.registerFontFamily("Body", normal="Body", bold="BodyBold", italic="BodyItalic", boldItalic="BodyBold")

PAGE_W, PAGE_H = A4
MARGIN = 36
CW = PAGE_W - 2 * MARGIN

C_GOLD = colors.HexColor("#805D15")
C_TINT = colors.HexColor("#F5F0E3")
C_BORDER = colors.HexColor("#D5C6A2")
C_INK = colors.HexColor("#1F2429")
C_SLATE = colors.HexColor("#545C64")
C_SKETCH = colors.HexColor("#1D3557")  # blueprint ink for easy-to-copy pen sketches

STYLES = {
    "title": ParagraphStyle("Title", fontName="DisplayBold", fontSize=16, leading=20, textColor=C_GOLD, spaceAfter=2),
    "sub": ParagraphStyle("Sub", fontName="BodyItalic", fontSize=8.5, leading=11, textColor=C_SLATE, spaceAfter=6),
    "h1": ParagraphStyle("H1", fontName="DisplayBold", fontSize=11.5, leading=14, textColor=C_GOLD, spaceBefore=7, spaceAfter=3, keepWithNext=True),
    "h2": ParagraphStyle("H2", fontName="BodyBold", fontSize=8.9, leading=11.5, textColor=C_INK, spaceBefore=4, spaceAfter=1.5, keepWithNext=True),
    "b": ParagraphStyle("B", fontName="Body", fontSize=8.3, leading=11.2, textColor=C_INK, leftIndent=10, firstLineIndent=-8, spaceAfter=1.5),
    "p": ParagraphStyle("P", fontName="Body", fontSize=8.3, leading=11.2, textColor=C_INK, spaceAfter=3),
    "cell": ParagraphStyle("Cell", fontName="Body", fontSize=7.9, leading=10.2, textColor=C_INK, spaceAfter=0),
    "cell_h": ParagraphStyle("CellH", fontName="BodyBold", fontSize=7.9, leading=10.2, textColor=C_GOLD, spaceAfter=0),
}


def p(t):
    return Paragraph(t, STYLES["p"])


def b(lead, t):
    return Paragraph(f"<font color='#805D15'>&bull;</font> <b>{lead}:</b> {t}", STYLES["b"])


def h1(t):
    return Paragraph(t, STYLES["h1"])


def h2(t):
    return Paragraph(t, STYLES["h2"])


def tbl(headers, rows, widths):
    data = [[Paragraph(h, STYLES["cell_h"]) for h in headers]]
    data += [[Paragraph(c, STYLES["cell"]) for c in r] for r in rows]
    t = Table(data, colWidths=widths, hAlign="LEFT")
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BACKGROUND", (0, 0), (-1, 0), C_TINT),
        ("GRID", (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
    ]))
    return t


def sketch_box_panel() -> Drawing:
    """Simple pen-style reference sketches easy to copy by hand in 2-3 minutes."""
    w, h = CW, 108
    d = Drawing(w, h)
    d.add(Rect(0, 0, w, h, rx=3, ry=3, fillColor=colors.HexColor("#FAF8F2"), strokeColor=C_BORDER, strokeWidth=0.5))

    # Left half: Hand-drawable 4-box Core Loop
    lx = 12
    d.add(String(lx + 115, h - 12, "SKETCH 1: CORE LOOP (draw in 1 min)", fontName="BodyBold", fontSize=7.2, fillColor=C_GOLD, textAnchor="middle"))
    bw, bh = 86, 24
    nodes = {
        "ACT": (lx + 6, h / 2 - bh / 2 - 4, "1. ACTION", "Dim/Flare, Push, Vein"),
        "RES": (lx + 74, h - 42, "2. RESULT", "Reveal & Toggle Traps"),
        "REW": (lx + 142, h / 2 - bh / 2 - 4, "3. REWARD", "Cross + Log (+1 Ember)"),
        "DEC": (lx + 74, 8, "4. DECISION", "Exit or Risk Detour"),
    }
    for k, (bx, by, t1, t2) in nodes.items():
        d.add(Rect(bx, by, bw, bh, rx=3, ry=3, fillColor=colors.white, strokeColor=C_SKETCH, strokeWidth=0.9))
        d.add(String(bx + bw / 2, by + 13.5, t1, fontName="BodyBold", fontSize=6.8, fillColor=C_SKETCH, textAnchor="middle"))
        d.add(String(bx + bw / 2, by + 4.5, t2, fontName="Body", fontSize=6.2, fillColor=C_INK, textAnchor="middle"))

    def arr(x1, y1, x2, y2):
        d.add(Line(x1, y1, x2, y2, strokeColor=C_SKETCH, strokeWidth=0.9))
        ang = math.atan2(y2 - y1, x2 - x1)
        ah, aw = 4.5, 2.2
        d.add(Polygon([
            x2, y2,
            x2 - ah * math.cos(ang) + aw * math.sin(ang), y2 - ah * math.sin(ang) - aw * math.cos(ang),
            x2 - ah * math.cos(ang) - aw * math.sin(ang), y2 - ah * math.sin(ang) + aw * math.cos(ang),
        ], fillColor=C_SKETCH, strokeColor=C_SKETCH, strokeWidth=0.4))

    arr(lx + 52, 66, lx + 72, 74)
    arr(lx + 162, 74, lx + 182, 66)
    arr(lx + 182, 36, lx + 162, 28)
    arr(lx + 72, 28, lx + 52, 36)

    # Divider
    mid_x = 262
    d.add(Line(mid_x, 8, mid_x, h - 8, strokeColor=C_BORDER, strokeWidth=0.6))

    # Right half: Hand-drawable Gameplay / HUD Layout Sketch
    rx = mid_x + 12
    rw = w - rx - 10
    d.add(String(rx + rw / 2, h - 12, "SKETCH 2: HUD & DIEGETIC SCENE (draw in 1 min)", fontName="BodyBold", fontSize=7.2, fillColor=C_GOLD, textAnchor="middle"))
    d.add(Rect(rx, 10, rw, h - 28, fillColor=colors.white, strokeColor=C_SKETCH, strokeWidth=0.9))
    # 4 ember slots top-left
    for idx in range(4):
        fill = C_GOLD if idx < 3 else colors.white
        d.add(Circle(rx + 12 + idx * 10, h - 26, 3.3, fillColor=fill, strokeColor=C_SKETCH, strokeWidth=0.7))
    d.add(String(rx + 56, h - 28.5, "3/4 Embers", fontName="Body", fontSize=6.2, fillColor=C_INK))
    # Ground + shelf
    d.add(Line(rx + 4, 24, rx + rw - 4, 24, strokeColor=C_SKETCH, strokeWidth=0.9))
    # Stick Light + Lantern glow circle
    px, py = rx + 36, 24
    d.add(Circle(px + 12, py + 24, 18, fillColor=None, strokeColor=C_GOLD, strokeWidth=0.7, strokeDashArray=[2, 2]))
    d.add(Line(px, py, px - 4, py + 10, strokeColor=C_SKETCH, strokeWidth=1.0))
    d.add(Line(px, py + 10, px + 4, py, strokeColor=C_SKETCH, strokeWidth=1.0))
    d.add(Line(px, py + 10, px, py + 24, strokeColor=C_SKETCH, strokeWidth=1.0))
    d.add(Circle(px, py + 28, 3.5, fillColor=colors.white, strokeColor=C_SKETCH, strokeWidth=0.9))
    d.add(Line(px, py + 20, px + 11, py + 22, strokeColor=C_SKETCH, strokeWidth=0.9))
    d.add(Rect(px + 11, py + 17, 4, 5, fillColor=C_GOLD, strokeColor=C_SKETCH, strokeWidth=0.6))
    d.add(String(px, 14, "Light + Lantern", fontName="Body", fontSize=5.8, fillColor=C_INK, textAnchor="middle"))
    # Ember Vein box
    vx = rx + 112
    d.add(Rect(vx, 24, 18, 16, fillColor=C_TINT, strokeColor=C_SKETCH, strokeWidth=0.8))
    d.add(String(vx + 9, 29, "Vein", fontName="BodyBold", fontSize=6.0, fillColor=C_SKETCH, textAnchor="middle"))
    d.add(String(vx + 9, 14, "[E] Power/Drain", fontName="Body", fontSize=5.8, fillColor=C_INK, textAnchor="middle"))
    # Archive desk
    ax = rx + 186
    d.add(Rect(ax, 24, 24, 14, fillColor=C_TINT, strokeColor=C_SKETCH, strokeWidth=0.8))
    d.add(Rect(ax + 5, 38, 14, 7, fillColor=colors.white, strokeColor=C_SKETCH, strokeWidth=0.7))
    d.add(String(ax + 12, 14, "Memento (Lore)", fontName="Body", fontSize=5.8, fillColor=C_INK, textAnchor="middle"))
    return d


def footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(C_BORDER)
    canvas.setLineWidth(0.5)
    canvas.line(MARGIN, 28, PAGE_W - MARGIN, 28)
    canvas.setFont("Body", 7.5)
    canvas.setFillColor(C_SLATE)
    canvas.drawString(MARGIN, 17, "The Lighten Road · 2-3 Hour Handwritten GDD Reference Sheet (Serket Template)")
    canvas.drawRightString(PAGE_W - MARGIN, 17, f"Page {doc.page} / 4")
    canvas.restoreState()


def build():
    s = []
    s.append(Paragraph("The Lighten Road — Handwritten GDD Blueprint (2–3 Hour Exam Guide)", STYLES["title"]))
    s.append(Paragraph("Short, clean bullet format structured 1:1 with the GDD Blank Template. Total target: ~950 words + 2 quick 1-min pen sketches — comfortable to write cleanly by hand in ~90–120 minutes.", STYLES["sub"]))

    s.append(tbl(
        ["Handwriting Time Budget (Suggested)", "Target Output"],
        [
            ["<b>0:00 – 0:20</b> Packaging, TOC, Intro | <b>0:20 – 1:05</b> Concept + Sketches | <b>1:05 – 1:40</b> Story, Art, Audio | <b>1:40 – 2:30</b> UX, Market, Tech", "~6–8 handwritten A4 pages"],
        ],
        [CW - 125, 125],
    ))

    # PACKAGING
    s.append(h1("Packaging & Table of Contents"))
    s.append(b("Title", "<b>The Lighten Road</b>"))
    s.append(b("Company / Team", "The Lighten Road Team — Furkan Aydın (Design), Siraç Göktuğ Şimşek (Dev), Kemal Berke Aşık (Dev), Can Mutluşahinoğlu (Art), Maksim Martynenko (Art)."))
    s.append(b("Splash Art (Describe or Sketch)", "High-contrast 2D silhouette of Light holding a warm glowing lantern inside a pitch-black ruined corporate archive."))
    s.append(b("Tagline / Hook", "<i>\"Manage every ember of a fading lantern, manipulate ancient circuits, and survive pitch-black corporate ruins to uncover the Fall.\"</i>"))
    s.append(p("<b>Table of Contents:</b> 1. Packaging · 2. Introduction · 3. Concept · 4. Story · 5. Art · 6. Audio · 7. Game Experience · 8. Market Requirements · 9. Technical Requirements"))

    # INTRODUCTION
    s.append(h1("1. Introduction"))
    s.append(b("Summary / Pitch", "A tense 2D side-scrolling atmospheric puzzle-platformer where condemned Ruin Researcher Light navigates pitch-black post-apocalyptic ruins using an ember lantern and Optical Lenses (Ethereal Cyan, Inverse Crimson) to manipulate Ember Veins, Conduit Pipes, and Light Mirrors."))
    s.append(b("Inspirations", "<i>Games:</i> Inside, Limbo (dark silhouette physics), Little Nightmares (scale), Dark Souls (stored async logs), Mass Effect (Codex). <i>Non-games:</i> Brutalist office ruins, chiaroscuro noir lighting, handwritten ledgers."))
    s.append(b("Player Experience", "Loneliness, fear of darkness, resource anxiety on every Flare, relief when an Ember Vein puzzle is solved, and quiet reward when discovering a hidden Memento yields story clarity."))
    s.append(b("Platform & Software", "PC (Windows/macOS), KB+Mouse/Xbox/PlayStation. Built in Unity 6000.3.2f1 (URP 2D Lighting, Input System, C#, TextMeshPro, NUnit, CC0 Foley)."))
    s.append(b("Genre & Target Audience", "2D Side-Scrolling Atmospheric Puzzle-Platformer. <i>Bartle Primary:</i> <b>Explorer</b> (lore detours, Mementos). <i>Secondary:</i> <b>Achiever</b> (tight ember optimization). Ages 16–32 indie fans."))

    # CONCEPT
    s.append(h1("2. Concept"))
    s.append(KeepTogether([sketch_box_panel(), Spacer(1, 3)]))
    s.append(b("Core Loop", "(1) ACTION: Scout in Dim/Flare, Power/Drain Ember Veins; push & climb. (2) RESULT: Reveal hazards; obstacles move/freeze/ghostify. (3) REWARD: Solve puzzle, scavenge Ember, unlock Archive Log. (4) NEW DECISION: Continue through level or risk detour for hidden Memento/lore."))
    s.append(b("Themes", "Vision vs. Survival; Curiosity & Lore (Mementos = story clarity, no ember); Coupled Consequences (Embers & Lenses reshape rooms)."))
    s.append(b("Primary Mechanics", "<b>Walking-Only Traversal:</b> Single walk speed (no sprint/double-jump/attack), 1-box jump, 2-box Ledge Climb, crate push. <b>Lantern (4-Ember Max / 3 Start):</b> Dim = free; Flare = -1 ember; >3s at 0 = Darkness Death; <b>The Flare Cast</b> in Level 4. <b>Normal (Orange) Ember:</b> State 1 (Powers/Opens) & State 0 (stops dead as static platform). <b>Lenses:</b> Ethereal (Cyan) removes collision (Ghosting); Inverse (Crimson) reverses machinery (State -1). <b>Pipes & Mirrors:</b> Rotate Conduit Pipes & 45-deg Light Mirrors (red beam melts Corrupted Objects, absorbed by Corrupted Mirrors)."))
    s.append(b("Secondary Mechanics (HW2 Memento + 3D Book)", "Diegetic desks trigger <code>SocialChannel.Raise(LogFound)</code> via <code>StoredDelivery</code> & <code>AmbientMembership</code> (03_Observer). Listeners update <i>ArchiveCodexMenu</i> (3D interactive pixel book) & <i>PartyFeedUI</i>—purely for lore (no ember granted)."))
    s.append(b("Tertiary Mechanics", "Sector checkpoints (restore 3 embers & reset room crates/circuits while preserving Memento history), single-cue dynamic device prompts, lantern fade-out chamber transitions."))
    s.append(b("Combat / AI / NPCs", "<b>No conventional combat</b>—danger is darkness starvation, hazards, and the Level 4 boss (The Flare Cast). <b>AI/NPCs:</b> Planned stalker/follower monster (tentative, pending animations) + disembodied <b>Archivist</b> voice."))
    s.append(b("Puzzles / Level Progression / Special Systems", "<b>No quests — 4-Level Progression only:</b> L1 Foundation (Admin Zone, State 1/0), L2 Lenses (Cyan/Crimson, Conduit Pipes, Skeleton Key Trap), L3 Mirrors (Secret Lab, Monochrome Zone, 45-deg Mirrors/Corrupted Objects), L4 Final Exam (Apex Core, 4 quadrants, Flare Cast). Special: Observer+Strategy bus & DualSense HID."))

    # STORY
    s.append(h1("3. Story"))
    s.append(b("Narrative", "<i>Beginning (L1 Admin Zone):</i> 400 years after The Fall, Light wakes in darkness to scavenge ruins for freedom. <i>Middle (L2–3 Secret Lab & Monochrome Zone):</i> Uncovers Lenses, Pipes, Mirrors, and Mementos. <i>End/Conflict (L4 Apex Core):</i> Confronts the Fall's source via The Flare Cast, torn between survival and curiosity."))
    s.append(b("Characters", "<b>Light (172 cm):</b> Gaunt, pale, unkempt dark hair, stows lantern on belt to climb; smart solver, no weapons. <b>The Archivist:</b> Voiced by Furkan Aydın / team (rewritten to match lore). <b>Pre-Fall Clerks:</b> Known via Memento records."))
    s.append(b("Dialogue", "No NPC dialogue trees; story lives in authored Memento records (Title + Story + Observation Clue) and stark death notifications."))

    # ART & AUDIO
    s.append(h1("4. Art & 5. Audio"))
    s.append(b("Art Design & Animation", "High-contrast 2D pixel art (Office Tilemap palette; organic Ember Veins in L1–L2 transitioning to industrial Conduit Pipes from L2 puzzle 4 / L3+) + pixel-rendered 3D Memento book. Character animations from Maksim's sheets (Push, Sheathing, Climb)."))
    s.append(b("VFX & Lighting", "URP 2D Lighting radial vision cones, glowing veins, sub-25% fuel flicker, lantern sparks, quantized 5-band 3D book shader. Reduced Motion disables shake/orbit/bob."))
    s.append(b("Music, SFX & Voice Acting", "Minimalist dark ambient music leaving room for quiet; curated CC0 foley cues (footsteps, match Flare, crackling embers, leather book turns); Archivist voice acting by Furkan / team; DualSense speaker audio."))

    # GAME EXPERIENCE
    s.append(h1("6. Game Experience"))
    s.append(b("UI / UX & Menus", "UI will be remade (design language kept flexible). <i>Menus & HUD:</i> Title Menu, Dual-Column Pause Menu, 4-Tab Settings (Audio, Display, Controller, Accessibility), Death Screen, Ember HUD."))
    s.append(tbl(
        ["Control Action", "Keyboard / Mouse", "Xbox", "PlayStation"],
        [
            ["Move / Ledge Climb", "A/D or Arrows · W/Up (climb)", "L-Stick/D-Pad · Up", "L-Stick/D-Pad · Up"],
            ["Jump / Flare / Interact", "Space / F / E", "A / RT / Y", "Cross / R2 / Triangle"],
            ["Archive / Page Curl / Pause", "Tab / Mouse Drag / Esc-P", "View / LB-RB / Menu", "Create / L1-R1 / Options"],
            ["Lens / Pipe / Mirror", "TBD (not finalized)", "TBD (not finalized)", "TBD (not finalized)"],
        ],
        [130, 140, 100, CW - 370],
    ))
    s.append(Spacer(1, 3))
    s.append(b("Diegetics & Integration", "Lantern halo = fuel state; glowing veins = circuit power; DualSense RGB lightbar mirrors lantern state; physical filing desks & 3D book. Accessibility: Reduced Motion, FPS cap, auto-switching device glyphs. Single-player only."))

    # MARKET & TECHNICAL
    s.append(h1("7. Market Requirements & 8. Technical Requirements"))
    s.append(b("MoSCoW Priorities", "<b>Must:</b> Walking/push/climb, 4-ember Dim/Flare, Power/Drain Veins & Lenses, Observer Memento, Level 1 & 2, Menus/Input parity. <b>Should:</b> 3D Book, Archivist voice acting, CC0 SFX, Reduced Motion, DualSense. <b>Could:</b> Level 3 & 4. <b>Want:</b> Remapping, Localization, Stalker AI."))
    s.append(b("MVP, Stretch & Delivery", "<i>MVP:</i> 2 of 4 explained levels (Level 1 & Level 2) + Observer Memento. <i>Stretch:</i> Full 4-level arc (L3 Secret Lab, L4 Apex Core). <i>Delivery/Marketing:</i> DGD3004 showcase + itch.io. <i>Post-Launch:</i> Telemetry tuning & Windows/macOS driver QA."))
    s.append(b("Known Issues & Systems", "Cross-owner circuit sync/drain-at-4-cap/retry reset merges in Kemal's mechanisms; <code>Office.unity</code> final exit linkage. Modular <code>.asmdef</code>s: <i>Lantern</i>, <i>Player</i>, <i>Observer</i>, <i>Codex</i>, <i>Frontend</i>, <i>Input</i>, <i>Audio</i>, <i>Interaction/Mechanisms</i>."))
    s.append(b("Work Packages & Tasks", "<b>Siraç (Dev):</b> Player, Lantern, Observer/Memento 3D Book, UI, DualSense, tests · <b>Kemal (Dev):</b> Veins, Shelves, Lifts · <b>Furkan (Design):</b> Levels, tuning, lore, voice · <b>Can (Art):</b> Tiles & lighting · <b>Maksim (Art):</b> Light character & sprites. <i>Passes:</i> Prototype -> Gameplay -> Polish -> Release."))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=MARGIN,
        rightMargin=MARGIN,
        topMargin=32,
        bottomMargin=36,
        title="The Lighten Road — 2-3 Hour Handwritten GDD Guide",
    )
    doc.build(s, onFirstPage=footer, onLaterPages=footer)
    copy2(OUTPUT, DESKTOP_OUT)
    print(f"Built {OUTPUT} ({doc.page} pages) and copied to {DESKTOP_OUT}")


if __name__ == "__main__":
    build()
