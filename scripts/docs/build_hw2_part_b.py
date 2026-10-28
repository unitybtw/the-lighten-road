"""Build the two-page Part B section from its editable Markdown source."""
from pathlib import Path
import re
from xml.sax.saxutils import escape
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'output/pdf/HW2_B_TheLightenRoad.pdf'
FONT_DIR = Path('/System/Library/Fonts/Supplemental')
pdfmetrics.registerFont(TTFont('Body', str(FONT_DIR/'Arial.ttf')))
pdfmetrics.registerFont(TTFont('BodyBold', str(FONT_DIR/'Arial Bold.ttf')))
pdfmetrics.registerFontFamily('Body', normal='Body', bold='BodyBold', italic='Body', boldItalic='BodyBold')
gold = colors.HexColor('#805d15')
ink = colors.HexColor('#252b31')
style = ParagraphStyle('Body', fontName='Body', fontSize=10, leading=13.5, textColor=ink, spaceAfter=8)
styles = {
    'title': ParagraphStyle('Title', parent=style, fontName='BodyBold',fontSize=21,leading=25,textColor=gold,spaceAfter=8),
    'h2': ParagraphStyle('H2',parent=style,fontName='BodyBold',fontSize=13.5,leading=17,textColor=gold,spaceBefore=11,spaceAfter=7,keepWithNext=True),
    'h3': ParagraphStyle('H3',parent=style,fontName='BodyBold',fontSize=11.3,leading=15,textColor=gold,spaceBefore=9,spaceAfter=7,keepWithNext=True),
    'small': ParagraphStyle('Small',parent=style,fontSize=8.7,leading=11.5,spaceAfter=6),
    'cell': ParagraphStyle('Cell',parent=style,fontSize=9.3,leading=12,spaceAfter=0),
    'step': ParagraphStyle('Step',parent=style,fontSize=9.5,leading=12.5,spaceAfter=4),
}
def rich(text):
    return re.sub(r'\*\*(.+?)\*\*',r'<b>\1</b>',escape(text))
def para(text, which='body'):
    return Paragraph(rich(text),style if which=='body' else styles[which])

def footer(canvas, doc):
    canvas.setStrokeColor(colors.HexColor('#ddd4c1'));canvas.setLineWidth(.5)
    canvas.line(44,38,A4[0]-44,38)
    canvas.setFont('Body',8);canvas.setFillColor(colors.HexColor('#62666a'))
    canvas.drawString(44,25,'The Lighten Road | DGD3004 | Homework 2 B')
    canvas.drawRightString(A4[0]-44,25,f'{doc.page} / 2')
    if doc.page>1:
        canvas.setFont('BodyBold',8);canvas.setFillColor(gold)
        canvas.drawString(44,A4[1]-32,'B.3 How it works in your game | continued')

lines=(ROOT/'docs/HW2-PART-B.md').read_text().splitlines()
story=[];i=0
while i<len(lines):
    line=lines[i].strip();i+=1
    if not line:continue
    if line=='<!-- pagebreak -->':story.append(PageBreak());continue
    if line.startswith('|'):
        rows=[line]
        while i<len(lines) and lines[i].startswith('|'):rows.append(lines[i]);i+=1
        rows=[row for row in rows if not re.match(r'^\|[\s|:-]+\|$',row)]
        cells=[[para(c.strip(),'cell') for c in row.strip('|').split('|')] for row in rows]
        widths=[129, A4[0]-88-129]
        table=Table(cells,colWidths=widths,hAlign='LEFT')
        table.setStyle(TableStyle([
            ('VALIGN',(0,0),(-1,-1),'TOP'),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#f2eddf')),
            ('GRID',(0,0),(-1,-1),.45,colors.HexColor('#d5c6a2')),
            ('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),
            ('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6),
        ]))
        story.extend([table,Spacer(1,6)]);continue
    if line.startswith('# '):story.append(para(line[2:],'title'))
    elif line.startswith('## '):story.append(para(line[3:],'h2'))
    elif line.startswith('### '):story.append(para(line[4:],'h3'))
    elif re.match(r'^\d+\. ',line):story.append(para(line,'step'))
    elif line.startswith('DGD3004') or line.startswith('Team:'):story.append(para(line,'small'))
    else:story.append(para(line))
OUTPUT.parent.mkdir(parents=True,exist_ok=True)
doc=SimpleDocTemplate(str(OUTPUT),pagesize=A4,rightMargin=44,leftMargin=44,topMargin=48,bottomMargin=49,title='The Lighten Road - Homework 2 B',author='The Lighten Road team')
doc.build(story,onFirstPage=footer,onLaterPages=footer)
print(OUTPUT)
