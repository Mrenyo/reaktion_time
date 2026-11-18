# -*- coding: utf-8 -*-
# A dolgozat szedése: jelölőnyelvű fejezetfájlok -> PDF (reportlab).
# Futtatás: python build.py <tartalom mappa> <ábra mappa> <kimeneti pdf>
import sys, os, re, io, hashlib
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import cm
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_JUSTIFY, TA_CENTER, TA_LEFT
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, PageBreak, Image, Table,
                                TableStyle, KeepTogether, Preformatted, NextPageTemplate, CondPageBreak)
from reportlab.platypus.tableofcontents import TableOfContents
from PIL import Image as PILImage
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

CONTENT, FIG, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
EQDIR = os.path.join(FIG, "eq"); os.makedirs(EQDIR, exist_ok=True)

F = r"C:\Windows\Fonts"
pdfmetrics.registerFont(TTFont("TNR", os.path.join(F, "times.ttf")))
pdfmetrics.registerFont(TTFont("TNR-B", os.path.join(F, "timesbd.ttf")))
pdfmetrics.registerFont(TTFont("TNR-I", os.path.join(F, "timesi.ttf")))
pdfmetrics.registerFont(TTFont("TNR-BI", os.path.join(F, "timesbi.ttf")))
pdfmetrics.registerFontFamily("TNR", normal="TNR", bold="TNR-B", italic="TNR-I", boldItalic="TNR-BI")
pdfmetrics.registerFont(TTFont("Mono", os.path.join(F, "consola.ttf")))
pdfmetrics.registerFont(TTFont("Mono-B", os.path.join(F, "consolab.ttf")))
pdfmetrics.registerFontFamily("Mono", normal="Mono", bold="Mono-B", italic="Mono", boldItalic="Mono-B")

TITLE = "Reakcióidőmérés Tobii szemmozgáskövetővel megtámogatva"
PAGE_W, PAGE_H = A4
LM, RM, TM, BM = 3.0 * cm, 2.5 * cm, 2.5 * cm, 2.5 * cm
TEXT_W = PAGE_W - LM - RM

body = ParagraphStyle("body", fontName="TNR", fontSize=12, leading=16, alignment=TA_JUSTIFY, spaceAfter=5, firstLineIndent=0)
bullet = ParagraphStyle("bullet", parent=body, leftIndent=22, bulletIndent=8, spaceAfter=2)
h1 = ParagraphStyle("h1", fontName="TNR-B", fontSize=17, leading=21, spaceBefore=0, spaceAfter=10, keepWithNext=1)
h2 = ParagraphStyle("h2", fontName="TNR-B", fontSize=13.5, leading=17, spaceBefore=10, spaceAfter=5, keepWithNext=1)
h3 = ParagraphStyle("h3", fontName="TNR-B", fontSize=12, leading=15, spaceBefore=7, spaceAfter=4, keepWithNext=1)
caption = ParagraphStyle("caption", fontName="TNR-I", fontSize=10, leading=12.5, alignment=TA_CENTER, spaceBefore=3, spaceAfter=8)
tcaption = ParagraphStyle("tcaption", fontName="TNR-I", fontSize=10, leading=12.5, alignment=TA_LEFT, spaceBefore=5, spaceAfter=3, keepWithNext=1)
cell = ParagraphStyle("cell", fontName="TNR", fontSize=9, leading=10.8, alignment=TA_LEFT)
cellh = ParagraphStyle("cellh", parent=cell, fontName="TNR-B")
code = ParagraphStyle("code", fontName="Mono", fontSize=7.5, leading=9.2, backColor=colors.HexColor("#F3F3F3"), borderColor=colors.HexColor("#CCCCCC"),
                      borderWidth=0.5, borderPadding=5, leftIndent=6, rightIndent=6, spaceBefore=4, spaceAfter=8)
refst = ParagraphStyle("ref", fontName="TNR", fontSize=9.5, leading=11.5, alignment=TA_LEFT, leftIndent=24, firstLineIndent=-24, spaceAfter=3)
mark_b = ParagraphStyle("markb", fontName="TNR", fontSize=10, leading=12.5, alignment=TA_LEFT, backColor=colors.HexColor("#FFF2CC"), borderColor=colors.HexColor("#BF9000"),
                        borderWidth=0.8, borderPadding=5, leftIndent=7, rightIndent=7, spaceBefore=6, spaceAfter=11)
mark_h = ParagraphStyle("markh", parent=mark_b, backColor=colors.HexColor("#FCE4D6"), borderColor=colors.HexColor("#C55A11"))
listitem = ParagraphStyle("listitem", fontName="TNR", fontSize=10.5, leading=13.5, alignment=TA_LEFT, leftIndent=30, firstLineIndent=-30, spaceAfter=6)
toc_styles = [ParagraphStyle("toc0", fontName="TNR-B", fontSize=11, leading=13.5, spaceBefore=4, leftIndent=0),
              ParagraphStyle("toc1", fontName="TNR", fontSize=10.5, leading=12.6, leftIndent=16),
              ParagraphStyle("toc2", fontName="TNR", fontSize=10.5, leading=13, leftIndent=40)]
lof_style = [ParagraphStyle("lof", fontName="TNR", fontSize=10, leading=12.5, leftIndent=0, spaceAfter=1)]


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def inline(s, mono_size=None):
    s = esc(s)
    parts = re.split(r"(`[^`]+`)", s)
    out = []
    for p in parts:
        if p.startswith("`") and p.endswith("`") and len(p) > 2:
            sz = f' size="{mono_size}"' if mono_size else ' size="10.5"'
            out.append(f'<font face="Mono"{sz}>{p[1:-1]}</font>')
        else:
            p = re.sub(r"\*([A-Za-z])\*(összes|aktív|inger|jelző)", r"<i>\1</i><sub>\2</sub>", p)
            p = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", p)
            p = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"<i>\1</i>", p)
            p = re.sub(r"<i>([A-Za-z])</i>(összes|aktív|inger|jelző)", r"<i>\1</i><sub>\2</sub>", p)
            out.append(p)
    return "".join(out)


class Heading(Paragraph):
    def __init__(self, text, style, level, key, toc_text):
        super().__init__(f'<a name="{key}"/>{text}', style)
        self.level, self.key, self.toc_text = level, key, toc_text


class Anchor(Paragraph):
    """Felirat, amely a jegyzékbe (ábra/táblázat) is bekerül."""
    def __init__(self, text, style, kind, key, list_text):
        super().__init__(f'<a name="{key}"/>{text}', style)
        self.kind, self.key, self.list_text = kind, key, list_text


class ListOf(TableOfContents):
    def __init__(self, kind, **kw):
        super().__init__(**kw); self.kind = kind

    def notify(self, kind, stuff):
        if kind == self.kind:
            self.addEntry(*stuff)


class Doc(BaseDocTemplate):
    def afterFlowable(self, fl):
        if isinstance(fl, Heading):
            if fl.level <= 1:
                self.notify("TOCEntry", (fl.level, fl.toc_text, self.page, fl.key))
            if fl.level <= 1:
                self.canv.bookmarkPage(fl.key)
                self.canv.addOutlineEntry(re.sub(r"<[^>]+>", "", fl.toc_text), fl.key, level=fl.level, closed=(fl.level == 0))
            if fl.level == 0:
                self.chapter_title = re.sub(r"<[^>]+>", "", fl.toc_text)
        elif isinstance(fl, Anchor):
            self.notify(fl.kind, (0, fl.list_text, self.page, fl.key))


def on_page(canv, doc):
    canv.saveState()
    canv.setFont("TNR", 10.5)
    canv.drawCentredString(PAGE_W / 2, 1.4 * cm, str(doc.page))
    canv.setFont("TNR-I", 9); canv.setFillColor(colors.HexColor("#555555"))
    canv.drawString(LM, PAGE_H - 1.6 * cm, TITLE)
    canv.setStrokeColor(colors.HexColor("#999999")); canv.setLineWidth(0.4)
    canv.line(LM, PAGE_H - 1.75 * cm, PAGE_W - RM, PAGE_H - 1.75 * cm)
    canv.restoreState()


def on_title(canv, doc):
    pass


def render_eq(tex):
    key = hashlib.md5(tex.encode("utf-8")).hexdigest()[:12]
    path = os.path.join(EQDIR, key + ".png")
    if not os.path.exists(path):
        with matplotlib.rc_context({"mathtext.fontset": "dejavuserif"}):
            fig = plt.figure(figsize=(0.1, 0.1))
            fig.text(0, 0, f"${tex}$", fontsize=13)
            fig.savefig(path, dpi=300, bbox_inches="tight", pad_inches=0.03, facecolor="white")
            plt.close(fig)
    w, h = PILImage.open(path).size
    return path, w / 300 * 72, h / 300 * 72


def wrap_code(text, width=104):
    out = []
    for line in text.replace("\t", "    ").split("\n"):
        while len(line) > width:
            cut = line.rfind(",", 0, width)
            cut = cut + 1 if cut > width * 0.5 else width
            out.append(line[:cut]); line = "    " + line[cut:]
        out.append(line)
    return "\n".join(out)


# --- elemzés ---------------------------------------------------------------
files = sorted(f for f in os.listdir(CONTENT) if re.match(r"^\d\d_.*\.md$", f))
story_main = []      # főszöveg
marks = {"B": [], "H": []}
state = {"chap": 0, "chap_label": "", "sec": 0, "sub": 0, "fig": 0, "tab": 0, "eq": 0, "app": 0, "numbered": False, "cur_sec": ""}
outline = []


MARK = re.compile(r"\[(BIZONYITAS|HIANYZO ADAT): ((?:[^\[\]]|\[[^\[\]]*\])*)\]")


def add_marks(text, out):
    """Bekezdés feldolgozása: a [BIZONYITAS: ...] és [HIANYZO ADAT: ...] jelölések külön dobozba kerülnek."""
    pat = re.compile(r"\[(BIZONYITAS|HIANYZO ADAT): ((?:[^\[\]]|\[[^\[\]]*\])*)\]")
    pos = 0
    for m in pat.finditer(text):
        before = text[pos:m.start()].strip()
        if before:
            out.append(Paragraph(inline(before), body))
        kind = "B" if m.group(1) == "BIZONYITAS" else "H"
        n = len(marks[kind]) + 1
        marks[kind].append((n, state["cur_sec"], m.group(2)))
        label = f"[BIZONYITAS {n}]" if kind == "B" else f"[HIANYZO ADAT {n}]"
        key = f"mark{kind}{n}"
        out.append(Paragraph(f'<a name="{key}"/><b>{label}</b> {inline(m.group(2), mono_size=9.5)}', mark_b if kind == "B" else mark_h))
        pos = m.end()
    rest = text[pos:].strip()
    if rest:
        out.append(Paragraph(inline(rest), body))


def make_table(cap, widths, rows, out):
    state["tab"] += 1
    num = f'{state["chap_label"]}.{state["tab"]}'
    key = f"tab{num}"
    data = []
    for i, r in enumerate(rows):
        cells = [c.strip() for c in r.split("|")]
        data.append([Paragraph(inline(c, mono_size=8.5), cellh if i == 0 else cell) for c in cells])
    ncol = max(len(r) for r in data)
    for r in data:
        while len(r) < ncol:
            r.append(Paragraph("", cell))
    if widths:
        w = [float(x) for x in widths.split(",")]
        tot = sum(w); w = [x / tot * TEXT_W for x in w]
    else:
        w = [TEXT_W / ncol] * ncol
    t = Table(data, colWidths=w, repeatRows=1)
    t.setStyle(TableStyle([
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#808080")),
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#DCE6F1")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 4), ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 3), ("BOTTOMPADDING", (0, 0), (-1, -1), 3.5),
    ]))
    out.append(CondPageBreak(3.5 * cm))
    out.append(Anchor(f"{num}. táblázat: {inline(cap)}", tcaption, "TOCTable", key, f"{num}. táblázat: {inline(cap)}"))
    out.append(t)
    out.append(Spacer(1, 8))


def make_fig(fn, width_cm, cap, out):
    state["fig"] += 1
    num = state["fig"]
    key = f"fig{num}"
    path = os.path.join(FIG, fn)
    iw, ih = PILImage.open(path).size
    w = min(float(width_cm) * 0.86 * cm, TEXT_W); h = w * ih / iw
    maxh = 13.0 * cm
    if h > maxh:
        w, h = w * maxh / h, maxh
    img = Image(path, width=w, height=h); img.hAlign = "CENTER"
    short = cap.split(". ")[0].rstrip(".")
    out.append(KeepTogether([Spacer(1, 3), img, Anchor(f"{num}. ábra – {inline(cap)}", caption, "TOCFigure", key, f"{num}. ábra – {inline(short)}")]))


def parse(fn, out):
    lines = open(os.path.join(CONTENT, fn), encoding="utf-8").read().split("\n")
    is_refs = fn.startswith("9") and "forras" in fn or "irodalomjegyzek" in fn
    i = 0
    while i < len(lines):
        ln = lines[i]
        if not ln.strip():
            i += 1; continue
        if is_refs:
            out.append(Paragraph(inline(ln.strip()), refst)); i += 1; continue
        if ln.startswith("```"):
            j = i + 1; buf = []
            while j < len(lines) and not lines[j].startswith("```"):
                buf.append(lines[j]); j += 1
            out.append(Preformatted(wrap_code("\n".join(buf)), code)); i = j + 1; continue
        if ln.startswith("!include "):
            txt = open(os.path.join(CONTENT, ln[9:].strip()), encoding="utf-8-sig").read().rstrip("\n")
            out.append(Preformatted(wrap_code(txt), code)); i += 1; continue
        if ln.startswith("!pagebreak"):
            out.append(PageBreak()); i += 1; continue
        if ln.startswith("!fig "):
            fnm, w, cap = [x.strip() for x in ln[5:].split("|", 2)]
            make_fig(fnm, w, cap, out); i += 1; continue
        if ln.startswith("!eq "):
            state["eq"] += 1
            path, w, h = render_eq(ln[4:].strip())
            sc = min(1.0, (TEXT_W - 2.2 * cm) / w)
            img = Image(path, width=w * sc, height=h * sc)
            num = Paragraph(f'({state["chap_label"]}.{state["eq"]})', ParagraphStyle("eqn", fontName="TNR", fontSize=11, alignment=2))
            t = Table([[img, num]], colWidths=[TEXT_W - 1.8 * cm, 1.8 * cm])
            t.setStyle(TableStyle([("ALIGN", (0, 0), (0, 0), "CENTER"), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 8)]))
            out.append(t); i += 1; continue
        if ln.startswith("!table "):
            head = ln[7:]
            cap, _, widths = head.partition("||")
            j = i + 1; rows = []
            while j < len(lines) and lines[j].strip():
                rows.append(lines[j]); j += 1
            make_table(cap.strip(), widths.strip(), rows, out); i = j; continue
        if ln.startswith("!list_bizonyitas") or ln.startswith("!list_hianyzo"):
            out.append(("LIST", "B" if "bizony" in ln else "H")); i += 1; continue
        m = re.match(r"^(#!|#A|#{1,3}) (.*)$", ln)
        if m:
            tag, text = m.group(1), m.group(2).strip()
            if tag in ("#", "#!", "#A"):
                state.update(sec=0, sub=0, tab=0, eq=0)
                if tag == "#":
                    state["chap"] += 1; state["chap_label"] = str(state["chap"]); label = f'{state["chap"]}. {text}'
                elif tag == "#A":
                    state["app"] += 1; state["chap_label"] = "ABCDEFGH"[state["app"] - 1]; label = f'{state["chap_label"]} melléklet – {text}'
                else:
                    state["chap_label"] = ""; label = text
                state["cur_sec"] = label.split(" ")[0] if tag == "#" else state["chap_label"]
                key = f"h{len(outline)}"
                outline.append((0, label))
                if tag == "#" and state["chap"] > 1:
                    out.append(Spacer(1, 16)); out.append(CondPageBreak(7 * cm))
                else:
                    out.append(PageBreak())
                out.append(Heading(esc(label), h1, 0, key, esc(label)))
            elif tag == "##":
                state["sec"] += 1; state["sub"] = 0
                num = f'{state["chap_label"]}.{state["sec"]}.' if state["chap_label"] else ""
                label = f"{num} {text}".strip(); state["cur_sec"] = num or state["cur_sec"]
                key = f"h{len(outline)}"; outline.append((1, label))
                out.append(Heading(esc(label), h2, 1, key, esc(label)))
            else:
                state["sub"] += 1
                num = f'{state["chap_label"]}.{state["sec"]}.{state["sub"]}.' if state["chap_label"] else ""
                label = f"{num} {text}".strip(); state["cur_sec"] = num or state["cur_sec"]
                key = f"h{len(outline)}"; outline.append((2, label))
                out.append(Heading(esc(label), h3, 2, key, esc(label)))
            i += 1; continue
        if re.match(r"^(- |\d+\. )", ln):
            while i < len(lines) and re.match(r"^(- |\d+\. )", lines[i]):
                mm = re.match(r"^(- |(\d+)\. )(.*)$", lines[i])
                bt = "•" if mm.group(1) == "- " else mm.group(2) + "."
                txt = mm.group(3); mk = MARK.search(txt)
                if mk:
                    out.append(Paragraph(inline((txt[:mk.start()] + txt[mk.end():]).strip()), bullet, bulletText=bt))
                    add_marks(mk.group(0), out)
                else:
                    out.append(Paragraph(inline(txt), bullet, bulletText=bt))
                i += 1
            out.append(Spacer(1, 4)); continue
        add_marks(ln.strip(), out); i += 1


main_files = [f for f in files if f[:2] < "90"]
ref_files = [f for f in files if f[:2] in ("90", "91")]
app_files = [f for f in files if f[:2] >= "95"]

front, mainst, refs, apps = [], [], [], []
parse(main_files[0], front)                     # kivonat
for f in main_files[1:]:
    parse(f, mainst)
for f in ref_files:
    parse(f, refs)
for f in app_files:
    parse(f, apps)

# a mellékletbeli listák feloldása
resolved = []
for fl in apps:
    if isinstance(fl, tuple) and fl[0] == "LIST":
        for n, sec, text in marks[fl[1]]:
            lab = f"B{n}" if fl[1] == "B" else f"H{n}"
            resolved.append(Paragraph(f'<b>{lab}</b>&nbsp;&nbsp;<a href="#mark{fl[1]}{n}" color="#1f4e79">({sec.rstrip(".")})</a> {inline(text, mono_size=9)}', listitem))
    else:
        resolved.append(fl)
apps = resolved

# --- összeállítás ----------------------------------------------------------
story = []
ts = lambda size, bold=False, sb=0, sa=0, it=False: ParagraphStyle("t", fontName="TNR-B" if bold else ("TNR-I" if it else "TNR"), fontSize=size, leading=size * 1.3, alignment=TA_CENTER, spaceBefore=sb, spaceAfter=sa)
story += [Spacer(1, 1.2 * cm), Paragraph("Pécsi Tudományegyetem", ts(18, True)), Paragraph("Műszaki és Informatikai Kar", ts(16)), Paragraph("Informatika és Villamos Intézet", ts(16)),
          Spacer(1, 4.2 * cm), Paragraph(TITLE, ts(24, True)), Spacer(1, 0.8 * cm),
          Paragraph("A mérőrendszer tervezése és megvalósítása", ts(15, it=True)), Paragraph("Műszaki dokumentáció", ts(15, it=True)),
          Spacer(1, 4.6 * cm), Paragraph("Készítette: <b>Mrenka Máté</b>", ts(14)), Spacer(1, 0.3 * cm),
          Paragraph("Belső konzulens: <b>Sebők-Tornai Bence</b>", ts(14)), Paragraph("Beosztása: tanársegéd", ts(14)),
          Spacer(1, 2.4 * cm), Paragraph("Pécs", ts(14)), Paragraph("2026", ts(14)), NextPageTemplate("main")]
story += front
toc = TableOfContents(); toc.levelStyles = toc_styles; toc.dotsMinLevel = 0
story += [PageBreak(), Paragraph("Tartalomjegyzék", h1), toc]
story += mainst
story += [Spacer(1, 16), CondPageBreak(8 * cm), Heading("Irodalomjegyzék", h1, 0, "hrefs", "Irodalomjegyzék"),
          Paragraph("Az [1]–[46] tétel a TMDK-dolgozat irodalomjegyzéke, változatlan sorrendben és számozással. A [47]–[59] tétel a jelen dolgozatban újonnan hivatkozott forrás; ezek elérhetőségét és adatait a beadás előtt ellenőrizni kell, és hozzáférési dátummal kell ellátni.", ParagraphStyle("refnote", parent=body, fontSize=9.5, leading=12, fontName="TNR-I"))]
story += refs
lof = ListOf("TOCFigure"); lof.levelStyles = lof_style; lof.dotsMinLevel = 0
lot = ListOf("TOCTable"); lot.levelStyles = lof_style; lot.dotsMinLevel = 0
story += [Spacer(1, 16), CondPageBreak(9 * cm), Heading("Ábrajegyzék", h1, 0, "hlof", "Ábrajegyzék"), lof]
story += apps

doc = Doc(OUT, pagesize=A4, leftMargin=LM, rightMargin=RM, topMargin=TM, bottomMargin=BM, title=TITLE, author="Mrenka Máté",
          subject="Reakcióidő-mérő rendszer műszaki dokumentációja", creator="reportlab")
frame = Frame(LM, BM, TEXT_W, PAGE_H - TM - BM, id="f", leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
doc.addPageTemplates([PageTemplate(id="title", frames=[frame], onPage=on_title), PageTemplate(id="main", frames=[frame], onPage=on_page)])
doc.multiBuild(story)
with open(os.path.join(os.path.dirname(OUT), "outline.txt"), "w", encoding="utf-8") as fo:
    for lvl, lab in outline:
        fo.write("  " * lvl + lab + "\n")
    fo.write(f"\nBIZONYITAS: {len(marks['B'])}, HIANYZO ADAT: {len(marks['H'])}, abrak: {state['fig']}\n")
print("kész:", OUT, "B:", len(marks["B"]), "H:", len(marks["H"]), "ábrák:", state["fig"])
