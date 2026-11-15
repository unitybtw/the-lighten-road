"""Build The Lighten Road Game Design Document (GDD Blank Template structure) as a polished PDF."""
from pathlib import Path
from shutil import copy2
from PIL import Image as PILImage
from reportlab.graphics.shapes import Drawing, Rect, String, Line, Polygon, Path as GPath
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_RIGHT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    CondPageBreak,
    Frame,
    Image,
    KeepTogether,
    NextPageTemplate,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)
from reportlab.platypus.tableofcontents import TableOfContents

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "output/pdf/The_Lighten_Road_GDD.pdf"
DESKTOP_OUT = Path.home() / "Desktop/The_Lighten_Road_GDD.pdf"
CACHE_DIR = Path("/tmp/tlr_gdd_assets")
FONT_DIR = Path("/System/Library/Fonts/Supplemental")

# Register crisp system typography (Georgia serif for titles/headings, Arial sans for dense body)
pdfmetrics.registerFont(TTFont("Display", str(FONT_DIR / "Georgia.ttf")))
pdfmetrics.registerFont(TTFont("DisplayBold", str(FONT_DIR / "Georgia Bold.ttf")))
pdfmetrics.registerFont(TTFont("DisplayItalic", str(FONT_DIR / "Georgia Italic.ttf")))
pdfmetrics.registerFontFamily(
    "Display",
    normal="Display",
    bold="DisplayBold",
    italic="DisplayItalic",
    boldItalic="DisplayBold",
)

pdfmetrics.registerFont(TTFont("Body", str(FONT_DIR / "Arial.ttf")))
pdfmetrics.registerFont(TTFont("BodyBold", str(FONT_DIR / "Arial Bold.ttf")))
pdfmetrics.registerFont(TTFont("BodyItalic", str(FONT_DIR / "Arial Italic.ttf")))
pdfmetrics.registerFontFamily(
    "Body",
    normal="Body",
    bold="BodyBold",
    italic="BodyItalic",
    boldItalic="BodyBold",
)

# Archive Palette
C_GOLD = colors.HexColor("#805D15")
C_GOLD_BRIGHT = colors.HexColor("#A67C24")
C_GOLD_PALE = colors.HexColor("#F5F0E3")
C_GOLD_BORDER = colors.HexColor("#D5C6A2")
C_INK = colors.HexColor("#1F2429")
C_SLATE = colors.HexColor("#545C64")
C_MUTED = colors.HexColor("#7A828A")
C_DARK_BG = colors.HexColor("#12161B")
C_LINE = colors.HexColor("#E2D8C0")

PAGE_W, PAGE_H = A4
MARGIN_X = 44
CONTENT_W = PAGE_W - 2 * MARGIN_X

STYLES = {
    "cover_kicker": ParagraphStyle(
        "CoverKicker", fontName="BodyBold", fontSize=8.5, leading=11,
        textColor=C_GOLD_BRIGHT, spaceAfter=6, alignment=TA_LEFT,
    ),
    "cover_title": ParagraphStyle(
        "CoverTitle", fontName="DisplayBold", fontSize=30, leading=34,
        textColor=C_INK, spaceAfter=4,
    ),
    "cover_sub": ParagraphStyle(
        "CoverSub", fontName="DisplayItalic", fontSize=13, leading=18,
        textColor=C_GOLD, spaceAfter=12,
    ),
    "cover_hook": ParagraphStyle(
        "CoverHook", fontName="Display", fontSize=10.5, leading=15,
        textColor=C_INK, spaceAfter=0,
    ),
    "h1": ParagraphStyle(
        "H1", fontName="DisplayBold", fontSize=18, leading=22,
        textColor=C_GOLD, spaceBefore=14, spaceAfter=6, keepWithNext=True,
    ),
    "h2": ParagraphStyle(
        "H2", fontName="DisplayBold", fontSize=12.5, leading=16,
        textColor=C_INK, spaceBefore=10, spaceAfter=4, keepWithNext=True,
    ),
    "h3": ParagraphStyle(
        "H3", fontName="BodyBold", fontSize=10, leading=13.5,
        textColor=C_GOLD, spaceBefore=7, spaceAfter=3, keepWithNext=True,
    ),
    "body": ParagraphStyle(
        "BodyText", fontName="Body", fontSize=9.4, leading=13.6,
        textColor=C_INK, spaceAfter=6,
    ),
    "bullet": ParagraphStyle(
        "Bullet", fontName="Body", fontSize=9.2, leading=13.2,
        textColor=C_INK, leftIndent=14, firstLineIndent=-10, spaceAfter=3.5,
    ),
    "cell": ParagraphStyle(
        "Cell", fontName="Body", fontSize=8.7, leading=12.0,
        textColor=C_INK, spaceAfter=0,
    ),
    "cell_head": ParagraphStyle(
        "CellHead", fontName="BodyBold", fontSize=8.7, leading=12.0,
        textColor=C_GOLD, spaceAfter=0,
    ),
    "caption": ParagraphStyle(
        "Caption", fontName="BodyItalic", fontSize=8.2, leading=11.0,
        textColor=C_SLATE, alignment=TA_CENTER, spaceBefore=4, spaceAfter=10,
    ),
    "callout": ParagraphStyle(
        "Callout", fontName="Body", fontSize=9.1, leading=13.0,
        textColor=C_INK, spaceAfter=0,
    ),
    "toc_h1": ParagraphStyle(
        "TOCH1", fontName="DisplayBold", fontSize=9.4, leading=11.5,
        textColor=C_INK, leftIndent=0, firstLineIndent=0, spaceBefore=2.5, spaceAfter=0.5,
    ),
    "toc_h2": ParagraphStyle(
        "TOCH2", fontName="Body", fontSize=8.1, leading=9.8,
        textColor=C_SLATE, leftIndent=14, firstLineIndent=0, spaceBefore=0, spaceAfter=0,
    ),
}


def cached_image(rel_path: str, max_w_px: int = 1280) -> Path:
    """Create a cached crisp downsampled copy so PDF size stays lean."""
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    src = ROOT / rel_path
    dst = CACHE_DIR / f"{src.stem}_{max_w_px}.jpg"
    if not dst.exists() or src.stat().st_mtime > dst.stat().st_mtime:
        with PILImage.open(src) as im:
            rgb = im.convert("RGB")
            if rgb.width > max_w_px:
                new_h = int(rgb.height * (max_w_px / rgb.width))
                rgb = rgb.resize((max_w_px, new_h), PILImage.Resampling.LANCZOS)
            rgb.save(dst, "JPEG", quality=90, optimize=True)
    return dst


def fig(rel_path: str, caption: str, width: float = CONTENT_W) -> KeepTogether:
    img_path = cached_image(rel_path)
    with PILImage.open(img_path) as im:
        aspect = im.height / im.width
    height = width * aspect
    frame = Table(
        [[Image(str(img_path), width=width - 4, height=height - 4)]],
        colWidths=[width],
        hAlign="CENTER",
    )
    frame.setStyle(TableStyle([
        ("BOX", (0, 0), (-1, -1), 0.75, C_GOLD_BORDER),
        ("BACKGROUND", (0, 0), (-1, -1), C_DARK_BG),
        ("LEFTPADDING", (0, 0), (-1, -1), 2),
        ("RIGHTPADDING", (0, 0), (-1, -1), 2),
        ("TOPPADDING", (0, 0), (-1, -1), 2),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 2),
    ]))
    return KeepTogether([Spacer(1, 3), frame, Paragraph(caption, STYLES["caption"])])


def fmt(text: str) -> str:
    return text.replace("<code>", "<font name='Courier' size='8.4' color='#3A4046'>").replace("</code>", "</font>")


def p(text: str, style: str = "body") -> Paragraph:
    return Paragraph(fmt(text), STYLES[style])


def b(lead: str, text: str) -> Paragraph:
    return Paragraph(fmt(f"<font color='#805D15'>&bull;</font> <b>{lead}:</b> {text}"), STYLES["bullet"])


def b_plain(text: str) -> Paragraph:
    return Paragraph(fmt(f"<font color='#805D15'>&bull;</font> {text}"), STYLES["bullet"])


def callout(title: str, body: str) -> Table:
    content = Paragraph(f"<b><font color='#805D15'>{title}</font></b><br/>{body}", STYLES["callout"])
    t = Table([[content]], colWidths=[CONTENT_W], hAlign="LEFT")
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), C_GOLD_PALE),
        ("LINEBEFORE", (0, 0), (0, -1), 2.5, C_GOLD),
        ("BOX", (0, 0), (-1, -1), 0.45, C_GOLD_BORDER),
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
        ("TOPPADDING", (0, 0), (-1, -1), 7),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
    ]))
    return t


def make_table(headers, rows, widths):
    data = [[Paragraph(fmt(h), STYLES["cell_head"]) for h in headers]]
    for r in rows:
        data.append([Paragraph(fmt(c), STYLES["cell"]) for c in r])
    t = Table(data, colWidths=widths, repeatRows=1, hAlign="LEFT")
    t.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BACKGROUND", (0, 0), (-1, 0), C_GOLD_PALE),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#FAF8F2")]),
        ("GRID", (0, 0), (-1, -1), 0.45, C_GOLD_BORDER),
        ("LINEBELOW", (0, 0), (-1, 0), 1.0, C_GOLD),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]))
    return t


def core_loop_diagram() -> Drawing:
    """Clean vector 4-stage Core Loop diagram matching the GDD design."""
    w, h = CONTENT_W, 188
    d = Drawing(w, h)
    cx = w / 2.0

    # Subtle outer frame
    d.add(Rect(0, 0, w, h, rx=4, ry=4, fillColor=colors.HexColor("#FAF8F2"), strokeColor=C_GOLD_BORDER, strokeWidth=0.6))

    box_w, box_h = 192, 50
    positions = {
        "RESULT": (cx - box_w / 2, h - 58),
        "NEW DECISION": (cx - box_w / 2, 10),
        "ACTION": (18, h / 2 - box_h / 2),
        "REWARD": (w - 18 - box_w, h / 2 - box_h / 2),
    }
    texts = {
        "ACTION": ("1. ACTION", "Scout in Dim/Flare; Power/Drain", "Ember Veins; push & climb."),
        "RESULT": ("2. RESULT", "Reveal hazards; obstacles", "move, freeze, or ghostify."),
        "REWARD": ("3. REWARD", "Solve the puzzle, scavenge", "Ember, unlock Archive Log."),
        "NEW DECISION": ("4. NEW DECISION", "Continue through level or detour", "for hidden lore & collectible."),
    }

    for key, (bx, by) in positions.items():
        d.add(Rect(bx, by, box_w, box_h, rx=5, ry=5, fillColor=colors.white, strokeColor=C_GOLD, strokeWidth=1.15))
        d.add(Rect(bx, by + box_h - 16, box_w, 16, rx=3, ry=3, fillColor=C_GOLD_PALE, strokeColor=None, strokeWidth=0))
        d.add(Line(bx, by + box_h - 16, bx + box_w, by + box_h - 16, strokeColor=C_GOLD_BORDER, strokeWidth=0.5))
        title, l1, l2 = texts[key]
        d.add(String(bx + box_w / 2, by + box_h - 11.5, title, fontName="BodyBold", fontSize=8.2, fillColor=C_GOLD, textAnchor="middle"))
        d.add(String(bx + box_w / 2, by + 18.5, l1, fontName="Body", fontSize=7.4, fillColor=C_INK, textAnchor="middle"))
        d.add(String(bx + box_w / 2, by + 8.5, l2, fontName="Body", fontSize=7.4, fillColor=C_INK, textAnchor="middle"))

    def draw_arrow(x1, y1, x2, y2):
        d.add(Line(x1, y1, x2, y2, strokeColor=C_GOLD_BRIGHT, strokeWidth=1.4))
        import math
        ang = math.atan2(y2 - y1, x2 - x1)
        ah = 6.0
        aw = 3.2
        px1 = x2 - ah * math.cos(ang) + aw * math.sin(ang)
        py1 = y2 - ah * math.sin(ang) - aw * math.cos(ang)
        px2 = x2 - ah * math.cos(ang) - aw * math.sin(ang)
        py2 = y2 - ah * math.sin(ang) + aw * math.cos(ang)
        d.add(Polygon([x2, y2, px1, py1, px2, py2], fillColor=C_GOLD_BRIGHT, strokeColor=C_GOLD_BRIGHT, strokeWidth=0.5))

    # Action -> Result -> Reward -> New Decision -> Action
    ax, ay = positions["ACTION"]
    rx, ry = positions["RESULT"]
    wx, wy = positions["REWARD"]
    nx, ny = positions["NEW DECISION"]

    draw_arrow(ax + box_w * 0.62, ay + box_h + 2, rx - 4, ry + box_h * 0.45)
    draw_arrow(rx + box_w + 4, ry + box_h * 0.45, wx + box_w * 0.38, wy + box_h + 2)
    draw_arrow(wx + box_w * 0.38, wy - 2, nx + box_w + 4, ny + box_h * 0.55)
    draw_arrow(nx - 4, ny + box_h * 0.55, ax + box_w * 0.62, ay - 2)
    return d


class GDDDocTemplate(BaseDocTemplate):
    def __init__(self, filename, **kwargs):
        self.total_pages_estimate = kwargs.pop("total_pages", 0)
        super().__init__(filename, **kwargs)
        frame = Frame(MARGIN_X, 46, CONTENT_W, PAGE_H - 96, id="normal", topPadding=0, bottomPadding=0, leftPadding=0, rightPadding=0)
        cover_frame = Frame(MARGIN_X, 42, CONTENT_W, PAGE_H - 80, id="cover", topPadding=0, bottomPadding=0, leftPadding=0, rightPadding=0)
        self.addPageTemplates([
            PageTemplate(id="Cover", frames=[cover_frame], onPage=self._draw_cover),
            PageTemplate(id="Body", frames=[frame], onPage=self._draw_chrome),
        ])
        self._bookmark_idx = 0
        self._has_h1 = False

    def _draw_cover(self, canvas, doc):
        canvas.saveState()
        # Clean top gold bar accent
        canvas.setFillColor(C_GOLD)
        canvas.rect(0, PAGE_H - 8, PAGE_W, 8, stroke=0, fill=1)
        canvas.setStrokeColor(C_LINE)
        canvas.setLineWidth(0.5)
        canvas.line(MARGIN_X, 34, PAGE_W - MARGIN_X, 34)
        canvas.setFont("Body", 7.8)
        canvas.setFillColor(C_MUTED)
        canvas.drawString(MARGIN_X, 21, "DGD3004 Game Development I · Game Design Document (Serket Template Structure)")
        canvas.drawRightString(PAGE_W - MARGIN_X, 21, "Updated 12 November 2026")
        canvas.restoreState()

    def _draw_chrome(self, canvas, doc):
        canvas.saveState()
        # Header
        canvas.setStrokeColor(C_LINE)
        canvas.setLineWidth(0.5)
        canvas.line(MARGIN_X, PAGE_H - 36, PAGE_W - MARGIN_X, PAGE_H - 36)
        canvas.setFont("DisplayBold", 8.2)
        canvas.setFillColor(C_GOLD)
        canvas.drawString(MARGIN_X, PAGE_H - 30, "THE LIGHTEN ROAD — GAME DESIGN DOCUMENT")
        canvas.setFont("Body", 7.8)
        canvas.setFillColor(C_MUTED)
        canvas.drawRightString(PAGE_W - MARGIN_X, PAGE_H - 30, "Unity 6000.3.2f1 · DGD3004")

        # Footer
        canvas.line(MARGIN_X, 34, PAGE_W - MARGIN_X, 34)
        canvas.setFont("Body", 8.0)
        canvas.setFillColor(C_SLATE)
        canvas.drawString(MARGIN_X, 21, "The Lighten Road Team · Pre-Production & Vertical Slice GDD")
        label = f"Page {doc.page} of {self.total_pages_estimate}" if self.total_pages_estimate else f"Page {doc.page}"
        canvas.drawRightString(PAGE_W - MARGIN_X, 21, label)
        canvas.restoreState()

    def handle_documentBegin(self):
        super().handle_documentBegin()
        self._has_h1 = False
        self._bookmark_idx = 0

    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph):
            text = flowable.getPlainText()
            sname = flowable.style.name
            if sname == "CoverTitle":
                key = f"sec_{self._bookmark_idx}"
                self._bookmark_idx += 1
                self._has_h1 = True
                self.canv.bookmarkPage(key)
                self.canv.addOutlineEntry("Packaging", key, level=0, closed=False)
                self.notify("TOCEntry", (0, "Packaging (Title, Company, Splash Art & Hook)", self.page, key))
            elif sname == "H1":
                key = f"sec_{self._bookmark_idx}"
                self._bookmark_idx += 1
                self._has_h1 = True
                self.canv.bookmarkPage(key)
                self.canv.addOutlineEntry(text, key, level=0, closed=False)
                self.notify("TOCEntry", (0, text, self.page, key))
            elif sname == "H2" and self._has_h1:
                key = f"sub_{self._bookmark_idx}"
                self._bookmark_idx += 1
                self.canv.bookmarkPage(key)
                self.canv.addOutlineEntry(text, key, level=1, closed=True)
                self.notify("TOCEntry", (1, text, self.page, key))


def h1(title: str) -> Paragraph:
    return Paragraph(title, STYLES["h1"])


def h2(title: str) -> Paragraph:
    return Paragraph(title, STYLES["h2"])


def h3(title: str) -> Paragraph:
    return Paragraph(title, STYLES["h3"])


def hr() -> Table:
    t = Table([[""]], colWidths=[CONTENT_W], rowHeights=[1])
    t.setStyle(TableStyle([("LINEABOVE", (0, 0), (-1, 0), 0.75, C_GOLD_BORDER)]))
    return t


def sec_break() -> list:
    return [CondPageBreak(175)]


def build_story():
    s = []

    # =========================================================================
    # PACKAGING (COVER PAGE — Template Page 1)
    # =========================================================================
    s.append(Paragraph("PACKAGING · DGD3004 GAME DEVELOPMENT I", STYLES["cover_kicker"]))
    s.append(Paragraph("The Lighten Road", STYLES["cover_title"]))
    s.append(Paragraph("Game Design Document — Updated Studio Edition", STYLES["cover_sub"]))

    s.append(callout(
        "Tagline / Hook",
        "Manage every ember of a fading lantern, manipulate ancient circuits, and uncover the truth behind the Fall inside pitch-black corporate ruins."
    ))
    s.append(Spacer(1, 10))

    s.append(fig(
        "development/sirac/previews/hud-clarity/flare-1920.png",
        "Splash Art — Light raising his lantern inside the decaying Corporate Archive (in-game URP 2D capture).",
        width=CONTENT_W,
    ))

    s.append(h2("Company / Development Team"))
    s.append(make_table(
        ["Attribute", "Details"],
        [
            ["Title", "<b>The Lighten Road</b>"],
            ["Company / Studio", "<b>The Lighten Road Team</b> (DGD3004 Game Development I)"],
            ["Team Roster", "Furkan Aydın (Game Designer) · Siraç Göktuğ Şimşek (Game Developer) · Kemal Berke Aşık (Game Developer) · Can Mutluşahinoğlu (Artist) · Maksim Martynenko (Artist)"],
            ["Engine & Target", "Unity 6000.3.2f1 (Universal 2D, Input System 1.17.0) · PC (Windows & macOS)"],
            ["Document Version", "v2.0 (Serket GDD Template Alignment + Vertical Slice Systems) · 12 November 2026"],
        ],
        [120, CONTENT_W - 120],
    ))

    # =========================================================================
    # TABLE OF CONTENTS (Page 2)
    # =========================================================================
    s.append(NextPageTemplate("Body"))
    s.append(PageBreak())
    s.append(Paragraph("Table of Contents", ParagraphStyle(
        "TOCTitle", fontName="DisplayBold", fontSize=18, leading=22, textColor=C_GOLD, spaceAfter=8
    )))
    s.append(p("<i>Organized in exact accordance with the Serket Studios GDD Blank Template hierarchy. Page numbers update dynamically across passes.</i>"))
    s.append(Spacer(1, 4))

    toc = TableOfContents()
    toc.levelStyles = [STYLES["toc_h1"], STYLES["toc_h2"]]
    toc.dotsMinLevel = 0
    toc.tableStyle = TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (-1, -1), 1),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 1),
    ])
    s.append(toc)

    # =========================================================================
    # 1. INTRODUCTION
    # =========================================================================
    s.append(PageBreak())
    s.append(h1("1. Introduction"))
    s.append(hr())

    s.append(h2("Summary / Pitch"))
    s.append(p(
        "<b>The Lighten Road</b> is a tense, atmospheric 2D side-scrolling puzzle-platformer in which a condemned Ruin Researcher named <b>Light</b> must carefully manage the embers of an anomalous lantern to survive pitch-black, post-apocalyptic ruins. Rather than focusing on combat, gameplay revolves around sightline scarcity, deliberate traversal, and bidirectional energy manipulation: holding passive <b>Dim</b> light to stay alive, spending <b>1 whole ember</b> on a <b>Flare</b>, transferring embers into <b>Ember Veins</b> (<b>Power vs. Drain</b>), and using <b>Lenses</b> (Ethereal & Inverse) to manipulate how Ember Veins and machinery behave."
    ))

    s.append(h2("Inspirations"))
    s.append(b("Other Games", "<i>Inside</i> & <i>Limbo</i> (crisp silhouette clarity, grounded physics crates, environmental tension); <i>Little Nightmares</i> (oppressive dark-scale rooms); <i>Dark Souls</i> & <i>Death Stranding</i> (asynchronous stored logs replayed to late joiners via <code>StoredDelivery</code>); <i>Mass Effect</i>, <i>Horizon</i> & <i>Destiny</i> (in-game Codex & world datapoints)."))
    s.append(b("Non-Games", "Decaying brutalist corporate office architecture, noir chiaroscuro lighting contrast, worn cloth-bound handwritten archive ledgers, and existential isolation 400 years after societal collapse."))

    s.append(h2("Player Experience"))
    s.append(p(
        "The player should feel what our world looks like 400 years after a catastrophic collapse: a crushing sense of loneliness, primal fear of total darkness, constant tension when committing scarce fuel just to see the floor ahead, relief when an Ember Vein puzzle is solved, and quiet reward when discovering a hidden memento yields story clarity."
    ))

    s.append(h2("Platform & Software"))
    s.append(make_table(
        ["Category", "Specification"],
        [
            ["Platforms", "PC (<b>Windows</b> & <b>macOS</b>), supporting Keyboard/Mouse, Xbox controllers, and full PlayStation DualSense / DualShock integration."],
            ["Game Engine", "<b>Unity 6000.3.2f1</b> (Universal 2D Template, URP 2D Lighting, TextMeshPro, Input System 1.17.0, NUnit Test Framework)."],
            ["Art & Shader Stack", "Point-filtered 2D pixel sprite sheets + custom fractional pixel-rendered 3D Archive Book (<code>RenderTexture</code> + <code>ArchiveBookSurface.shader</code>)."],
            ["Audio & Narration", "67 curated CC0 44.1 kHz mono foley cues, atmospheric music manager, offline neural TTS caching (<code>kokoro-fp16-george-v2</code>), and DualSense Bluetooth HID <code>0x31</code>/<code>0x35</code> Opus speaker packets."],
            ["Engineering Tooling", "Modular <code>.asmdef</code> C# assemblies, Python CI integrity verifier (<code>check_project.py</code>), Python/ReportLab doc pipelines."],
        ],
        [120, CONTENT_W - 120],
    ))

    s.append(Spacer(1, 6))
    s.append(h2("Genre"))
    s.append(p("2D Side-Scrolling Atmospheric Puzzle-Platformer."))

    s.append(h2("Target Audience"))
    s.append(b("Primary (Bartle Explorer)", "Players driven to explore pitch-black detours, inspect decaying ruins, and recover all hidden Old World Archive logs (targeting 100% Archive Decryption)."))
    s.append(b("Secondary (Bartle Achiever)", "Players rewarded by mastering the tight 4-ember fuel economy, optimizing jumps and crate routes, and solving coupled Power/Drain circuit puzzles cleanly."))
    s.append(b("Demographics & Comparable Titles", "Ages 16–32, PC and indie console players who enjoy deliberate atmospheric puzzle-platformers (<i>Inside</i>, <i>Limbo</i>, <i>Little Nightmares</i>) over fast combat action."))

    # =========================================================================
    # 2. CONCEPT
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("2. Concept"))
    s.append(hr())

    s.append(h2("Core Loop"))
    s.append(p(
        "Each room follows a repeatable four-step loop. Exiting a solved sector seals the doorway behind Light and drops him directly into the darkness of the next stage, carrying remaining embers forward so fuel conservation creates cumulative pressure across chambers:"
    ))
    s.append(Spacer(1, 4))
    s.append(core_loop_diagram())
    s.append(Spacer(1, 6))

    s.append(h2("Themes & Elemental Tetrad"))
    s.append(b("Vision vs. Survival", "Light is life and information, yet flaring spends irreplaceable fuel. Darkness is both blinding and fatal."))
    s.append(b("Curiosity & Lore", "Detouring to inspect a hidden Memento yields story clarity and quiet narrative reward—without affecting ember survival."))
    s.append(b("Coupled Consequence", "Powering or manipulating machinery with Embers and Lenses reshapes the room—every resource commit changes how obstacles behave."))
    s.append(Spacer(1, 3))
    s.append(make_table(
        ["Elemental Tetrad", "Design Summary"],
        [
            ["<b>Mechanics</b>", "Walking-only 2D precision puzzle-platforming, dynamic vision radius, crate/climb traversal, Ember Veins (State 1/0), Optical Lenses (Ethereal Cyan, Inverse Crimson), Conduit Pipes, and Light Mirrors."],
            ["<b>Story</b>", "Condemned Ruin Researcher <i>Light</i> seeks freedom scavenging pre-Fall corporate ruins and uncovers the truth behind The Fall."],
            ["<b>Aesthetics</b>", "High-contrast pixel chiaroscuro: pitch-black voids framed by warm lantern glow, flickering embers, and an industrial transition from organic Ember Veins to Conduit Pipes."],
            ["<b>Technology</b>", "Unity 6000.3.2f1, C#, Tilemap, URP 2D Lighting, point-filtered 3D Book shader, and Observer + Strategy event architecture."],
        ],
        [115, CONTENT_W - 115],
    ))

    s.append(h2("Primary Mechanics"))
    s.append(b("Walking-Only Traversal & Physics", "Single calibrated walking speed (sprint retired to preserve methodical pacing and remove endurance clutter), variable-height jumping (1-box jump arc, 2-box vertical ledge grab), physical crate pushing, and deliberate <b>Ledge Climb</b> (stowing the lantern onto the belt to climb solid ledges via Up input, cancellable via Down or Jump). <b>No sprint, no double-jump, and no attacks.</b>"))
    s.append(b("Lantern & 4-Ember Economy", "Maximum lantern capacity is <b>4 embers</b>; chambers start and respawn with <b>3 embers</b>. <b>Dim</b> provides a passive close halo at 0 fuel cost while at least 1 ember remains. Pressing <b>Flare</b> permanently consumes <b>1 whole ember</b> immediately for a bright, fading wide beam. Spending down to <b>0 embers</b> triggers a <b>3.0-second Darkness Death</b> timer unless any ember is recovered or game is paused. In the final encounter, <b>The Flare Cast</b> roots Light in place to supercharge the lantern."))
    s.append(b("Normal (Orange) Ember Veins", "Reversible 1-ember transfer. <b>State 1 (Power)</b> deposits 1 ember into a circuit (rolling shelves patrol, lifts winch up, mechanisms activate). <b>State 0 (Drain / Power Theft)</b> pulls the ember back into the lantern and stops the object dead in place so it can be used as a static platform."))
    s.append(b("Optical Lenses (Ethereal Cyan & Inverse Crimson)", "<b>Ethereal (Cyan) Lens:</b> Removes physical collision (Ghosting) so Light or light beams can pass through, without changing the object's kinetic state. <b>Inverse (Crimson) Lens:</b> Reverses machinery behavior (<b>State -1</b>)."))
    s.append(b("Conduit Pipes & Light Mirrors", "<b>Conduit Pipes:</b> Rotate-able pipes (Light locks in place; directional input selects a pipe and confirm rotates it). <b>Light Mirrors & Corrupted Objects:</b> Light locks in place to rotate mirror angles and bounce a 45-degree red beam; red beams melt shadow-webbed Corrupted Objects, while pulsing Corrupted Mirrors absorb the beam. <b>Thematic Transition:</b> Levels 1 and most of 2 feature natural, bulging organic Ember Veins; on the 4th puzzle of Level 2, veins tuck inside heavy industrial Conduit Pipes; from Level 3 onward, every Ember Vein is piped."))

    s.append(h2("Secondary Mechanics"))
    s.append(p(
        "Our chosen Homework 2 system is <b>Lore Collectibles / Memento</b> (from the <i>World & Discovery</i> tab), integrated into our 3D UI presentation:"
    ))
    s.append(b("Observer-Driven Memento Archive", "Interacting with a Memento / Archive Desk (<code>DiegeticWhisper</code>) invokes <code>CodexSession.Discover(id)</code>, publishing <code>SocialEventKind.LogFound</code> on <code>SocialChannel</code> with <code>StoredDelivery</code> and <code>AmbientMembership</code>. Listeners update the dossier, <b>Archive Decryption %</b>, and discovery feed—purely for lore and narrative clarity (no ember is granted)."))
    s.append(b("Interactive 3D Pixel-Rendered Book", "Pressing Interact or <code>Tab / View / Create</code> opens <code>ArchiveBook.prefab</code> inside a half-resolution point-filtered 3D overlay. A live 2048x1464 handwriting canvas prints directly onto deformable paper geometry with seamless spine folding, any-edge mouse page curling, real-time reverse spread previews, and a tucked ribbon bookmark."))

    s.append(fig(
        "development/sirac/previews/archive-book-refined-open.png",
        "Secondary Mechanics — Interactive 3D pixel-filtered Memento Archive displaying Archive Decryption %, Field Observation clue, and Archivist voice playback.",
        width=CONTENT_W * 0.86,
    ))

    s.append(h2("Tertiary Mechanics"))
    s.append(b("Checkpoints & World Consistency", "Sector checkpoints restore 3 embers and player position on failure; crates and room puzzles reset reliably without erasing Memento progress."))
    s.append(b("Single-Cue Prompts", "<code>ControllerGlyphService</code> detects deliberate Keyboard, Xbox, or PlayStation input and renders crisp device-specific hints without stacking cluttered tutorial walls."))
    s.append(b("Lantern-Led Transitions", "Chamber completion fades the lantern down into dark slate, holds brief chapter typography, and relights smoothly in the following sector."))

    s.append(h2("Combat"))
    s.append(p("<b>None.</b> Light has no weapons or conventional attacks. Danger stems from darkness starvation (>3s at 0 embers), abyssal drops, environmental hazards, and the final boss encounter (resolved via <b>The Flare Cast</b>)."))

    s.append(h2("Puzzles"))
    s.append(b("Ember & Lens Logic", "Combining Normal (Orange) Ember State 1/0 with the Ethereal (Cyan) Lens (ghosting collision) and Inverse (Crimson) Lens (State -1 reversal) to reshape traversal."))
    s.append(b("Conduit Pipes & Light Mirrors", "Rotating Conduit Pipes to route power and angling Light Mirrors to bounce 45-degree red beams onto Corrupted Objects while avoiding Corrupted Mirrors."))
    s.append(b("Compound Logic & Spatial Detours", "Solving multi-step rooms (such as the Skeleton Key Trap) and weighing detours to hidden Mementos for lore."))

    s.append(h2("Quests & Progression"))
    s.append(b("No Quests — Level Progression Only", "The game has <b>no quests</b>; progression is strictly linear across <b>4 levels</b>, paired with optional Memento discovery:"))
    s.append(b("Level 1: The Foundation (Administrative Zone)", "Teaches 1-box jump arc, 2-box vertical ledge grab, and Normal (Orange) Ember State 1 & State 0 (Power Theft)."))
    s.append(b("Level 2: The Lenses (Administrative Zone -> Secret Lab Transition)", "Introduces Ethereal (Cyan) Lens, Inverse (Crimson) Lens, and Conduit Pipes; tested via the <i>Skeleton Key</i> Trap and Compound Logic."))
    s.append(b("Level 3: The Mirrors (The Secret Lab)", "Features the <b>Monochrome Zone</b> (lantern disabled, pure-survival platforming in darkness), Projector Veins & Light Mirrors (45-degree red beam), Corrupted Objects vs. Corrupted Mirrors, and Systemic Integration."))
    s.append(b("Level 4: The Final Exam (The Apex Core)", "Introduces <b>The Flare Cast</b> (rooted, cinematic supercharge of the lantern to destroy the boss) across 4 simultaneous quadrants without tutorials."))

    s.append(h2("AI / NPCs"))
    s.append(p("A <b>stalker/follower monster</b> is planned throughout the game (tentative, pending character animations and implementation). Otherwise, automated hazards operate deterministically via machinery state, accompanied by the disembodied voice of <b>The Archivist</b> narrating past documents."))

    s.append(h2("Special Systems"))
    s.append(b("Observer + Strategy Event Bus (03_Observer)", "Reuses lecture <code>SocialChannel</code>, <code>StoredDelivery</code>, and <code>AmbientMembership</code> patterns to ensure replay-safe log reconstruction and zero coupling between world terminals and UI systems."))
    s.append(b("DualSense Bluetooth HID Pipeline", "Direct macOS <code>0x31</code>/<code>0x35</code> HID integration mirroring lantern states via RGB Lightbar colors, dynamic envelope haptics, adaptive R2 resistance, and filtered Opus micro-speaker audio."))

    # =========================================================================
    # 3. STORY
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("3. Story"))
    s.append(hr())

    s.append(h2("Narrative"))
    s.append(b("Beginning (Level 1 — The Administrative Zone)", "Four hundred years after a world-ending catastrophe called <b>The Fall</b>, condemned Ruin Researcher <b>Light</b> awakens in darkness. Bound by sentence to scavenge lost Old World technology for his freedom, he ignites his lantern and traverses the Administrative Zone, learning basic Ember Vein manipulation."))
    s.append(b("Middle (Levels 2 & 3 — The Secret Lab & Monochrome Zone)", "Descending from the Administrative Zone into the classified <b>Secret Lab</b>, Light uncovers Optical Lenses, industrial Conduit Pipes, and Light Mirrors, surviving the pitch-black <b>Monochrome Zone</b> where his lantern is disabled. Scattered Mementos reveal the truth behind pre-Fall experiments."))
    s.append(b("End & Conflict (Level 4 — The Apex Core)", "In the <b>Apex Core</b>, Light confronts the source of The Fall across four simultaneous quadrants and unleashes <b>The Flare Cast</b>—supercharging his lantern to destroy the boss. The central conflict pits survival caution against relentless curiosity."))

    s.append(CondPageBreak(260))
    s.append(h2("Characters & Relationships"))
    s.append(fig(
        "art/maksim/concepts/light-character-concept.jpg",
        "Character Design Sheet (Maksim Martynenko) — Light (172 cm), pale dead-like complexion from sunless ruins, dark unkempt hair, and hip ember pouch.",
        width=CONTENT_W * 0.88,
    ))
    s.append(make_table(
        ["Character", "Traits, Strengths & Weaknesses", "Motivation & Relationships"],
        [
            [
                "<b>Light</b><br/><i>Protagonist</i>",
                "172 cm gaunt Ruin Researcher; stows lantern onto belt to climb. <b>Strengths:</b> nimble climber, methodical circuit solver. <b>Weaknesses:</b> no conventional weapons; cannot survive >3s without embers.",
                "Driven by survival and earning freedom; his sole physical companion is his fragile lantern."
            ],
            [
                "<b>The Archivist</b><br/><i>Disembodied Voice</i>",
                "Voiced by Furkan Aydın / team (rewritten to match lore). Never appears onscreen.",
                "Articulates past Memento entries aloud when Light opens the Archive, acting as an echo of pre-Fall bureaucracy."
            ],
            [
                "<b>Keepers & Clerks</b><br/><i>Pre-Fall Personnel</i>",
                "Heard only through written records (<code>keepers-note</code>, <code>upper-archive</code>, <code>office-ledger</code>, <code>office-margin</code>).",
                "Leave practical warnings about shelf circuits, flooded currents, and rationing embers for the return trip."
            ],
        ],
        [95, 210, CONTENT_W - 305],
    ))

    s.append(Spacer(1, 6))
    s.append(h2("Dialogue"))
    s.append(p("There are no branching NPC conversations. Narrative text enters exclusively via authored <b>Memento</b> records (title, location tag, multi-page handwriting body, and actionable observation clue), subtle contextual cues, and stark death notifications (<i>\"The light went out\"</i>, <i>\"You fell\"</i>, <i>\"Caught in a hazard\"</i>)."))

    # =========================================================================
    # 4. ART
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("4. Art"))
    s.append(hr())

    s.append(h2("Design"))
    s.append(b("Dimension & Perspective", "Pure <b>2D side-scrolling</b> high-contrast pixel art paired with a point-filtered <b>3D interactive Memento Archive</b> stage."))
    s.append(b("Character & Environment Art", "Character sprites are built from Maksim's concept sheets (including idle, walk, jump, grounded <i>Push</i>, <i>Sheathing</i>, and <i>Climb</i> animations). Environments are built from Can's Office Tile Palette, distinguishing elevation planes with cool slate-blue/gray floors, crumbling rubble, filing cabinets, reception desks, and shattered windows—and transitioning visually from organic Ember Veins to industrial Conduit Pipes."))

    s.append(fig(
        "development/sirac/previews/office-annex/push-1920.png",
        "Level & Art Pass (Office Annex) — Point-filtered pixel tilemap, grounded Push contact pose, warm overhead fixture contrast, and minimal Ember HUD.",
        width=CONTENT_W * 0.90,
    ))

    s.append(h2("Visual Effects"))
    s.append(b("Particles & Embers", "Rising lantern sparks (<code>LanternSparksFX</code>), warm radial expansion pulses on first ignition & chamber completion, and flickering flame instability below 25% fuel."))
    s.append(b("Accessibility Restraint", "Under <b>Reduced Motion</b>, floating prompt bobs, orb orbits, screen shakes, and decorative pulses stop cleanly without removing clarity."))

    s.append(h2("Lighting"))
    s.append(p(
        "Powered by <b>URP 2D Lighting</b> (<code>Light2D</code>): absolute abyssal blacks yield only to Light's personal warm amber lantern radius, pale cold desk lamps above Archive Terminals, and glowing Ember Vein conduits. In 3D Archive mode, <code>ArchiveBookSurface.shader</code> quantizes lighting into 5 diffuse bands and a 5-bit color ramp with a procedural spine gutter fold shadow so 3D book geometry matches 2D pixel craft."
    ))

    s.append(h2("Animation"))
    s.append(p(
        "Driven at fixed physics cadence by <code>PlayerSpriteAnimator</code> and <code>PlayerActionWalkLayer</code>. Ledge climbing derives exact 2D pixel coordinates for hands, leading feet, and landing soles from authored sprite pixels—pinning the hand to the corner, lifting over the lip, and landing seamlessly without collider scaling or snapping."
    ))

    # =========================================================================
    # 5. AUDIO
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("5. Audio"))
    s.append(hr())

    s.append(h2("Music"))
    s.append(p(
        "Minimalist dark ambient score managed by <code>BackgroundMusicManager</code>. Music leaves deliberate breathing room for silence so darkness feels oppressive and environmental foley stands out clearly across transitions."
    ))

    s.append(h2("Sound Effects"))
    s.append(b("Curated CC0 Foley Cues", "All synthetic beeps/waveforms were retired in favor of edited, human-recorded CC0 foley (<code>archive-sfx-manifest.json</code>): leather & paper book page turns, cloth menu hover/confirm cues, surface-specific footsteps with repetition avoidance, real match ignition for Flare, fire crackle for breaking embers, mechanical latch surges for Veins, and an intensifying heartbeat under critical fuel."))
    s.append(b("DualSense Micro-Speaker", "Game cues for ignition, extinguish, pickup, and ember break are streamed over Bluetooth HID <code>0x35</code> reports without rattling the controller transducer."))

    s.append(h2("Voice Acting"))
    s.append(p(
        "<b>The Archivist</b> voices recovered Memento documents through recorded voice acting by Furkan Aydın and the team (rewritten to match the lore). Players can Play, Pause, or Replay spoken entries inside the 3D Archive while keeping audio continuous across page flips."
    ))

    # =========================================================================
    # 6. GAME EXPERIENCE
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("6. Game Experience"))
    s.append(hr())

    s.append(h2("UI / UX & Menus"))
    s.append(p(
        "The UI is planned for a full visual remake, so specific design language and color tokens are kept flexible. Navigation shares unified focus ownership between mouse hover, keyboard arrows, and gamepad D-pad/sticks."
    ))
    s.append(fig(
        "development/sirac/previews/pause-controller-fix/home-1920.png",
        "Pause Menu Layout — Dual-column layout pairing action navigation (left) with live chamber name, lantern flame ember count, and memento recovery status (right).",
        width=CONTENT_W * 0.88,
    ))
    s.append(b("Menus & HUD", "<b>Title Menu</b> (Start, Chapters, Options, Button Guide, Credits, Quit); <b>Dual-Column Pause Menu</b> (safe confirmations before Restart / Return to Title); <b>4-Tab Settings</b> (Audio, Display, Controller, Accessibility); <b>Death Screen</b>; and the <b>Ember HUD</b>."))

    s.append(h2("Controls"))
    s.append(make_table(
        ["Action", "Keyboard / Mouse", "Xbox Controller", "PlayStation Controller"],
        [
            ["Move", "A / D or Left / Right", "Left Stick / D-Pad", "Left Stick / D-Pad"],
            ["Ledge Climb", "W or Up Arrow (at ledge)", "D-Pad Up / Stick Up", "D-Pad Up / Stick Up"],
            ["Jump", "Space", "A", "Cross"],
            ["Lantern Flare", "F", "RT (>= 65% pull)", "R2 (>= 65% pull)"],
            ["Interact / Ember Vein / Memento", "E", "Y", "Triangle"],
            ["Open / Close Memento Archive", "Tab", "View", "Create"],
            ["Turn 3D Archive Pages", "Mouse Left-Drag (any edge)", "LB / RB or D-Pad", "L1 / R1 or D-Pad"],
            ["Pause / Back", "Esc / P", "Menu / B", "Options / Circle"],
            ["Lens / Pipe / Mirror Select & Confirm", "TBD (not finalized)", "TBD (not finalized)", "TBD (not finalized)"],
        ],
        [135, 130, 105, CONTENT_W - 370],
    ))

    s.append(Spacer(1, 6))
    s.append(h2("Diegetics"))
    s.append(p(
        "The lantern itself communicates survival radius and critical flicker; pulsing neon cables expose powered Ember Veins in world space; filing desks carry warm reading lamps; the physical 3D Codex turns paper pages under the mouse cursor; and the DualSense RGB Lightbar acts as a live physical mirror of the lantern in the player's hands (Warm Amber <code>#F29424</code>, Radiant Flare Cyan <code>#5AE6FF</code>, Pickup Mint <code>#26FFA6</code>, Critical Crimson Heartbeat <code>#F2140D</code>, Pause Slate <code>#3359A6</code>)."
    ))

    s.append(h2("Integration (Accessibility, Tooling & Scope)"))
    s.append(b("Accessibility", "Dedicated <i>Accessibility</i> settings with <b>Reduced Motion</b>, non-color-dependent state labels & audio/haptic cues, explicit glyph style override (Auto/KB/Xbox/PS), FPS cap selector (60 / 120 / Unlimited), and adjustable analog stick deadzone."))
    s.append(b("Mode & Tooling", "Strictly single-player atmospheric experience (no multiplayer chat/lobbies). Chapter progression accessible via <i>Realms & Chapters</i>; survival driven by the 3.0s zero-ember darkness countdown."))

    # =========================================================================
    # 7. MARKET REQUIREMENTS
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("7. Market Requirements"))
    s.append(hr())

    s.append(h2("Priorities (MoSCoW)"))
    s.append(make_table(
        ["Priority", "Scope Items"],
        [
            ["<b>Must Have</b>", "Walking/jumping/pushing/climbing controller; 4-ember Dim/Flare & 3s darkness death; Ember Vein Power/Drain circuits and Optical Lenses; Observer Memento Archive; Level 1 & Level 2 playable chapters; Menus & KB/Gamepad parity."],
            ["<b>Should Have</b>", "3D pixel-filtered interactive Memento Archive Book with any-edge page curl; Archivist voice acting; curated CC0 foley SFX cues; Reduced Motion accessibility; DualSense haptics/lightbar/speaker."],
            ["<b>Could Have</b>", "Level 3 (<i>The Secret Lab</i> & Monochrome Zone) and Level 4 (<i>The Apex Core</i>); expanded audio logs; chapter completion & decryption summary screen."],
            ["<b>Want to Have</b>", "Full runtime button-remapping screen; multi-language Memento localization; optional stalker/follower monster AI."],
        ],
        [95, CONTENT_W - 95],
    ))

    s.append(Spacer(1, 6))
    s.append(h2("Minimum Viable Product (MVP)"))
    s.append(p(
        "The MVP delivers <b>2 of the 4 explained levels (Level 1: The Foundation & Level 2: The Lenses)</b> in high-contrast 2D pixel art, proving all core movement mechanics, the 4-ember lantern & darkness death loop, Ember Vein & Lens machinery, and the Observer Memento Archive."
    ))

    s.append(h2("Stretch Goals"))
    s.append(p("Full 4-level arc extending through Level 3 (<i>The Mirrors / Secret Lab</i>) and Level 4 (<i>The Final Exam / Apex Core</i>), plus stalker/follower monster AI and localized subtitles."))

    s.append(h2("Delivery, Marketing & Post-Launch"))
    s.append(b("Delivery", "Course submission & showcase build for <b>DGD3004 Game Development I</b> targeting standalone Windows and macOS executables built cleanly from Unity 6000.3.2f1."))
    s.append(b("Marketing", "<b>itch.io</b> page and DGD3004 showcase spotlighting high-contrast pixel chiaroscuro GIFs: Flare reveals in absolute pitch black, tactical Power/Drain & Lens flips, and 3D Memento Archive page-turn interactions."))
    s.append(b("Post-Launch", "Analyze chamber completion times, death coordinates, and remaining exit ember budgets from playtests; resolve circuit edge cases; verify standalone controller drivers across Windows USB and macOS Bluetooth."))

    # =========================================================================
    # 8. TECHNICAL REQUIREMENTS
    # =========================================================================
    s.extend(sec_break())
    s.append(h1("8. Technical Requirements"))
    s.append(hr())

    s.append(h2("Known Issues"))
    s.append(b("Cross-Owner Circuit Integration", "Per the Mechanics Contract, shared two-node <code>EmberVein</code> state synchronization, full-inventory <code>Drain</code> guards (at 4/4 embers), mid-path freeze on <code>RollingShelf</code>, gravity descent on <code>SimpleLift</code>, and chamber-retry circuit resets require final cross-owner merge inside Kemal's mechanism scripts."))
    s.append(b("Chapter Linkage & Standalone Hardware", "<code>Office.unity</code> exit sequence currently completes the room and awaits final destination assignment; standalone Windows/USB DualSense matrix requires hands-on QA outside the macOS Editor."))

    s.append(h2("Systems (Modular Architecture)"))
    s.append(make_table(
        ["Assembly / Subsystem", "Key Classes & Responsibility"],
        [
            ["<code>TheLightenRoad.Lantern</code>", "<code>LanternFuel</code> (8-unit / 4-ember bank), <code>LanternController</code> (Dim/Flare), <code>EmberPickup</code>."],
            ["<code>TheLightenRoad.Player</code>", "<code>PlayerMotor2D</code>, <code>PlayerLedgeClimb</code>, <code>PlayerSpriteAnimator</code>, <code>FollowCamera2D</code>, <code>PlayerRespawnController</code>, <code>ChamberCompletionSequence</code>."],
            ["<code>TheLightenRoad.Observer</code>", "Lecture <code>SocialChannel</code>, <code>ISocialListener</code>, <code>SocialEvent</code> (+<code>LogFound</code>), <code>StoredDelivery</code> (<code>isReplay</code>), <code>AmbientMembership</code>, <code>PartyFeedUI</code>."],
            ["<code>TheLightenRoad.Codex</code> & <code>Frontend</code>", "<code>ArchiveLog</code>, <code>ArchiveCatalog</code>, <code>CodexSession</code>, <code>ArchiveCodexMenu</code>, <code>ArchiveBookPresentation</code>, <code>ArchiveBookPageTurn</code>, <code>ArchiveNarration</code>, <code>MainMenuController</code>, <code>PauseMenuController</code>."],
            ["<code>TheLightenRoad.Input</code> & <code>Audio</code>", "<code>ControllerGlyphService</code>, <code>DualSenseLightbar</code>, <code>DualSenseSpeaker</code>, <code>DualSenseTextures</code>, <code>BackgroundMusicManager</code>, <code>SoundEffectManager</code>."],
            ["<code>Interaction</code> & <code>Mechanisms</code>", "<code>EmberVein</code>, <code>RollingShelf</code>, <code>SimpleLift</code>, Lens/Pipe/Mirror controllers."],
        ],
        [155, CONTENT_W - 155],
    ))

    s.append(Spacer(1, 6))
    s.append(h2("Work Packages & Tasks"))
    s.append(make_table(
        ["Team Member", "Role", "Tasks & Accountability"],
        [
            ["<b>Siraç Göktuğ Şimşek</b>", "Game Developer", "WP1, WP2, WP4, WP5: Player traversal/climb/push, Lantern API, 3D Memento Archive & Observer architecture, UI/Menus, DualSense HID, PlayMode tests."],
            ["<b>Kemal Berke Aşık</b>", "Game Developer", "WP3: Shared multi-node <code>EmberVein</code> circuits, patrolling <code>RollingShelf</code>, gravity <code>SimpleLift</code>, chamber-retry resets."],
            ["<b>Furkan Aydın</b>", "Game Designer", "WP1–WP6 Design: Core loop specs, 4-ember tuning, chamber puzzle layouts, Memento narrative prose & Archivist voice acting, playtest balancing."],
            ["<b>Can Mutluşahinoğlu</b>", "Artist", "WP1, WP3, WP5 Art: Environmental pixel tilesets (Office palette), props, furniture, room lighting mood."],
            ["<b>Maksim Martynenko</b>", "Artist", "WP1, WP5 Art: Protagonist <i>Light</i> concept art & pixel study, animation sprite sheets, ember visual design."],
        ],
        [115, 85, CONTENT_W - 200],
    ))

    s.append(Spacer(1, 6))
    s.append(h2("Activities & Production Passes"))
    s.append(b("Pass 1 — Prototype Pass", "Movement sandbox, Lantern Fuel API, greybox Ember Vein interaction tests."))
    s.append(b("Pass 2 — Gameplay Pass", "Level 1 & Level 2 playable routes, Optical Lenses, Conduit Pipes, Observer Memento Archive."))
    s.append(b("Pass 3 — Polish Pass", "3D Archive Book shader & drag physics, curated CC0 foley SFX cues, UI remake, walking-only balance, DualSense integration."))
    s.append(b("Pass 4 — Demo & Release Pass", "Cross-owner circuit merge, full end-to-end chamber chaining, playtest telemetry pass, and Windows/macOS standalone builds."))

    return s


def main():
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    # Pre-pass to measure exact page count cleanly
    probe = GDDDocTemplate("/tmp/tlr_probe_gdd.pdf", pagesize=A4)
    probe.multiBuild(build_story())
    total = probe.page

    doc = GDDDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        total_pages=total,
        title="The Lighten Road — Game Design Document",
        author="The Lighten Road Team (DGD3004)",
    )
    doc.multiBuild(build_story())
    copy2(OUTPUT, DESKTOP_OUT)
    print(f"Built {OUTPUT} ({doc.page} pages) and copied to {DESKTOP_OUT}")


if __name__ == "__main__":
    main()
