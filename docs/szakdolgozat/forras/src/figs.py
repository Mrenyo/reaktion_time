# Ábrák a dolgozathoz (matplotlib). Futtatás: python figs.py <scratchpad>
import sys, os, csv, math
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle, Circle, FancyArrowPatch, Polygon
from PIL import Image, ImageDraw, ImageFont

S = sys.argv[1]
OUT = os.path.join(S, "fig")
SRC = os.path.join(S, "out")
os.makedirs(OUT, exist_ok=True)
plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 9, "savefig.dpi": 220})

BLUE, LBLUE, GREY, LGREY = "#1f4e79", "#dce6f1", "#555555", "#f2f2f2"
ORANGE, LORANGE, GREEN, LGREEN, RED = "#c55a11", "#fbe5d6", "#2e7d32", "#e2efda", "#b71c1c"


def box(ax, x, y, w, h, text, fc=LBLUE, ec=BLUE, fs=9, bold=False, lw=1.2):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0.02,rounding_size=0.08", fc=fc, ec=ec, lw=lw))
    ax.text(x + w / 2, y + h / 2, text, ha="center", va="center", fontsize=fs, fontweight="bold" if bold else "normal")


def arrow(ax, p, q, text=None, color=GREY, ls="-", fs=8, off=(0, 0.12), style="-|>", lw=1.2, rad=0.0):
    ax.add_patch(FancyArrowPatch(p, q, arrowstyle=style, mutation_scale=11, color=color, lw=lw, ls=ls,
                                 connectionstyle=f"arc3,rad={rad}", shrinkA=0, shrinkB=0))
    if text:
        ax.text((p[0] + q[0]) / 2 + off[0], (p[1] + q[1]) / 2 + off[1], text, ha="center", va="center", fontsize=fs, color=color)


def new(w, h, xl, yl):
    fig, ax = plt.subplots(figsize=(w, h))
    ax.set_xlim(0, xl); ax.set_ylim(0, yl); ax.axis("off")
    return fig, ax


def save(fig, name):
    fig.savefig(os.path.join(OUT, name), bbox_inches="tight", facecolor="white", pad_inches=0.05)
    plt.close(fig)
    print("ok", name)


# 1. Rendszer blokkvázlata -------------------------------------------------
fig, ax = new(8.6, 5.0, 17, 9)
box(ax, 0.2, 6.0, 5.2, 2.7, "PC – vezérlőalkalmazás\n(.NET 10, WinForms)\npróbák vezérlése, inger kirajzolása,\nscanout-korrekció, érvényesség,\nmentés, statisztika", fs=8)
box(ax, 6.6, 6.6, 4.0, 2.1, "Monitor\ninger + fotodióda-jelző\n(egy képkockában)", fc=LGREY, ec=GREY, fs=8)
box(ax, 12.0, 6.6, 4.8, 2.1, "Résztvevő\nlátás → döntés → kattintás", fc=LGREEN, ec=GREEN, fs=8)
box(ax, 6.6, 3.3, 4.0, 1.9, "BPW34 fotodióda +\ntranszimpedancia-erősítő", fc=LORANGE, ec=ORANGE, fs=8)
box(ax, 12.0, 3.3, 4.8, 1.9, "Egér bal gombjának\nmikrokapcsolója +\njelillesztő áramkör", fc=LORANGE, ec=ORANGE, fs=8)
box(ax, 6.6, 0.2, 10.2, 2.0, "ESP32-S3 (N16R8) – időmérő egység, ESP-IDF firmware\nközös időalap (esp_timer, µs): t_fény (ADC, GPIO4), t_gomb (GPIO5)\nRT = t_gomb − (t_fény + scanout-korrekció)", fs=8)
box(ax, 0.2, 0.2, 5.2, 2.0, "Tobii Pro szemkövető\ntekintetminták (USB)\n→ a próba érvényessége", fc=LGREEN, ec=GREEN, fs=8)
arrow(ax, (5.4, 7.6), (6.6, 7.6), "videojel", off=(0, 0.25), fs=7)
arrow(ax, (10.6, 7.6), (12.0, 7.6), "fény", off=(0, 0.25), fs=7)
arrow(ax, (8.6, 6.6), (8.6, 5.2), "a jelző fénye", off=(1.05, 0), fs=7, color=ORANGE)
arrow(ax, (14.4, 6.6), (14.4, 5.2), "kattintás", off=(0.85, 0), fs=7, color=GREEN)
arrow(ax, (8.6, 3.3), (8.6, 2.2), "analóg feszültség", off=(1.35, 0), fs=7, color=ORANGE)
arrow(ax, (14.4, 3.3), (14.4, 2.2), "aktív alacsony jel", off=(1.35, 0), fs=7, color=ORANGE)
arrow(ax, (4.4, 6.0), (6.6, 1.7), None, color=BLUE)
arrow(ax, (6.6, 0.9), (3.6, 6.0), None, color=BLUE)
ax.text(2.6, 4.2, "USB–UART, 115200 baud\n→ CHECK, korrekció [µs], RTIME\n← OK, RT [µs] vagy NA", fontsize=7, color=BLUE, ha="center", bbox=dict(fc="white", ec="none", pad=1))
arrow(ax, (0.9, 2.2), (0.9, 6.0), "USB", off=(-0.45, -1.3), fs=7, color=GREEN)
save(fig, "fig_rendszer.png")

# 2. Késleltetési lánc: szoftveres vs. hardveres végpontok --------------------
fig, ax = new(7.2, 3.2, 100, 44)
y1, y2 = 30, 12
ax.plot([4, 96], [y1, y1], color=GREY, lw=1); ax.plot([4, 96], [y2, y2], color=GREY, lw=1)
ax.text(2, y1 + 9.5, "Szoftveres (PC) időbélyegek", fontsize=9, fontweight="bold", color=RED)
ax.text(2, y2 - 10.5, "Fizikai események (ESP időalapján mérve)", fontsize=9, fontweight="bold", color=GREEN)
ev = [(10, "rajzolás\nvisszatér", y1, RED), (86, "egérüzenet\nfeldolgozása", y1, RED), (30, "a jelző/inger\nfénye megjelenik", y2, GREEN), (72, "a mikrokapcsoló\nzár", y2, GREEN)]
for x, t, y, c in ev:
    ax.plot([x, x], [y - 2.5, y + 2.5], color=c, lw=2.2)
    ax.text(x, y + (4.2 if y == y1 else -4.2), t, ha="center", va="bottom" if y == y1 else "top", fontsize=7.5, color=c)
ax.annotate("", xy=(86, y1 - 5), xytext=(10, y1 - 5), arrowprops=dict(arrowstyle="<->", color=RED, lw=1.4))
ax.text(48, y1 - 3.6, "RT(PC) = emberi reakcióidő + megjelenítési késés + inputkésés", ha="center", fontsize=8, color=RED)
ax.annotate("", xy=(72, y2 + 5), xytext=(30, y2 + 5), arrowprops=dict(arrowstyle="<->", color=GREEN, lw=1.4))
ax.text(51, y2 + 6.3, "RT(ESP) = emberi reakcióidő", ha="center", fontsize=8, color=GREEN)
ax.add_patch(Rectangle((10, y2 - 1.2), 20, 2.4, fc=LORANGE, ec=ORANGE, lw=0.8)); ax.text(20, y2, "megjelenítés", ha="center", va="center", fontsize=6.5)
ax.add_patch(Rectangle((72, y2 - 1.2), 14, 2.4, fc=LORANGE, ec=ORANGE, lw=0.8)); ax.text(79, y2, "USB, OS", ha="center", va="center", fontsize=6.5)
ax.plot([10, 10], [y2 + 1.2, y1 - 2.5], color=GREY, lw=0.6, ls=":"); ax.plot([86, 86], [y2 + 1.2, y1 - 2.5], color=GREY, lw=0.6, ls=":")
ax.text(97, y1, "t", fontsize=9, va="center"); ax.text(97, y2, "t", fontsize=9, va="center")
save(fig, "fig_lanc.png")

# 3. Firmware állapotgép -------------------------------------------------------
fig, ax = new(7.2, 2.6, 14, 5)
xs = [0.3, 3.9, 7.5, 11.1]
names = ["TETLEN", "VAR_FENY", "VAR_GOMB", "KESZ"]
subs = ["nincs próba", "fekete → fehér\nátmenetre vár", "az első\ngomblenyomásra vár", "RT lekérhető\n(RTIME)"]
for x, n, s in zip(xs, names, subs):
    box(ax, x, 1.9, 2.6, 1.5, n + "\n" + s, fs=8)
arrow(ax, (2.9, 2.65), (3.9, 2.65)); ax.text(3.4, 3.75, "korrekció\nérkezik", ha="center", fontsize=7, color=GREY)
arrow(ax, (6.5, 2.65), (7.5, 2.65)); ax.text(7.0, 3.75, "sötét, majd\nADC ≥ küszöb\n(t_fény)", ha="center", fontsize=7, color=GREY)
arrow(ax, (10.1, 2.65), (11.1, 2.65)); ax.text(10.6, 3.75, "új lenyomás\n(t_gomb)", ha="center", fontsize=7, color=GREY)
arrow(ax, (12.4, 1.9), (5.2, 1.9), None, rad=-0.28, color=BLUE)
arrow(ax, (8.8, 1.9), (5.6, 1.9), None, rad=-0.35, color=BLUE)
ax.text(8.8, 0.35, "új korrekció (új próba): bármely állapotból vissza VAR_FENY-be, az előző próba adatai eldobva", ha="center", fontsize=7.5, color=BLUE)
save(fig, "fig_allapotgep.png")

# 4. Egy próba idődiagramja ----------------------------------------------------
fig, ax = plt.subplots(figsize=(7.2, 3.9))
T = np.linspace(0, 100, 2001)
def step(t0, t1=None, lo=0, hi=1):
    y = np.where(T >= t0, hi, lo)
    if t1 is not None: y = np.where(T >= t1, lo, y)
    return y
rows = [
    ("PC → ESP (soros)", None),
    ("Jelző a képernyőn", step(45, 78)),
    ("Fotodióda-jel (ADC)", np.clip((T - 45) / 2.5, 0, 1) * (T < 78) + np.clip(1 - (T - 78) / 6, 0, 1) * (T >= 78)),
    ("Gomb (GPIO5, akt. alacsony)", 1 - step(72, 90)),
    ("PC által észlelt kattintás", step(78, 79.2)),
    ("ESP állapot", None),
]
n = len(rows)
for i, (lab, y) in enumerate(rows):
    base = (n - 1 - i) * 1.6
    ax.text(-2.5, base + 0.45, lab, ha="right", va="center", fontsize=8)
    if y is not None:
        ax.plot(T, base + y * 0.9, color=BLUE, lw=1.3)
        if "ADC" in lab:
            ax.plot([0, 100], [base + 0.45, base + 0.45], color=ORANGE, lw=0.8, ls="--")
            ax.text(100.5, base + 0.45, "küszöb", fontsize=6.5, color=ORANGE, va="center")
b = (n - 1) * 1.6
for x, t in [(12, "korrekció\n[µs]"), (79.5, "RTIME")]:
    ax.annotate("", xy=(x, b + 0.1), xytext=(x, b + 0.9), arrowprops=dict(arrowstyle="-|>", color=BLUE))
    ax.text(x + (1 if x < 50 else -1), b + 0.5, t, fontsize=7, va="center", ha="left" if x < 50 else "right", color=BLUE)
ax.annotate("", xy=(88, b + 0.9), xytext=(88, b + 0.1), arrowprops=dict(arrowstyle="-|>", color=GREEN))
ax.text(89, b + 0.5, "RT [µs]\nvagy NA", fontsize=7, va="center", color=GREEN)
for x0, x1, t, c in [(0, 12, "TETLEN", LGREY), (12, 46.3, "VAR_FENY", LBLUE), (46.3, 72, "VAR_GOMB", LORANGE), (72, 100, "KESZ", LGREEN)]:
    ax.add_patch(Rectangle((x0, 0.05), x1 - x0, 0.8, fc=c, ec=GREY, lw=0.6)); ax.text((x0 + x1) / 2, 0.45, t, ha="center", va="center", fontsize=7)
for x, c in [(46.3, ORANGE), (72, GREEN)]:
    ax.plot([x, x], [0.9, b - 0.2], color=c, lw=0.8, ls=":")
ax.text(46.3, b - 0.05, "t_fény", ha="center", fontsize=7.5, color=ORANGE); ax.text(72, b - 0.05, "t_gomb", ha="center", fontsize=7.5, color=GREEN)
ax.annotate("", xy=(72, -0.55), xytext=(46.3, -0.55), arrowprops=dict(arrowstyle="<->", color=RED, lw=1.3))
ax.text(59, -1.0, "RT(ESP) = t_gomb − (t_fény + korrekció)", ha="center", fontsize=8, color=RED)
ax.annotate("", xy=(45, -1.75), xytext=(12, -1.75), arrowprops=dict(arrowstyle="<->", color=GREY, lw=1))
ax.text(28.5, -2.2, "előperiódus (véletlen hosszú)", ha="center", fontsize=7.5, color=GREY)
ax.annotate("", xy=(100, -1.75), xytext=(78, -1.75), arrowprops=dict(arrowstyle="<->", color=GREY, lw=1))
ax.text(89, -2.2, "≥ 200 ms a következő próbáig", ha="center", fontsize=7.5, color=GREY)
ax.set_xlim(-42, 110); ax.set_ylim(-2.6, n * 1.6 + 0.2); ax.axis("off")
save(fig, "fig_proba.png")

# 5. Rétegek -------------------------------------------------------------------
fig, ax = new(8.0, 4.2, 18, 8)
box(ax, 6.2, 6.8, 5.6, 0.9, "Program.cs – composition root", fc=LGREY, ec=GREY, fs=8)
box(ax, 0.2, 4.5, 5.6, 1.6, "UI\nWinForms nézetek, Testreszabás,\nkijelölők, StagePainter", fs=8)
box(ax, 12.2, 4.5, 5.6, 1.6, "Infrastructure\nTobii, ESP/soros port, CSV, JSON,\nképidőzítés, EDID", fs=8)
box(ax, 5.9, 2.4, 6.2, 1.6, "Measurement\npresenterek, TrialRunner, TrialEvaluator,\ntekintetszabályok, interfészek", fc=LORANGE, ec=ORANGE, fs=8)
box(ax, 5.9, 0.2, 6.2, 1.6, "Domain\nbeállítások, eredmények, geometria,\nscanout-képlet, próbaterv", fc=LGREEN, ec=GREEN, fs=8)
box(ax, 13.6, 0.5, 4.2, 0.9, "Diagnostics – napló", fc=LGREY, ec=GREY, fs=8)
arrow(ax, (6.8, 6.8), (3.6, 6.1)); arrow(ax, (11.2, 6.8), (14.4, 6.1))
arrow(ax, (3.6, 4.5), (5.9, 3.6)); arrow(ax, (14.4, 4.5), (12.1, 3.6), ls="--")
ax.text(15.3, 3.7, "megvalósítja az\ninterfészeit", fontsize=7, color=GREY, ha="center")
arrow(ax, (9.0, 2.4), (9.0, 1.8))
arrow(ax, (1.6, 4.5), (5.9, 0.9), rad=0.25); arrow(ax, (16.9, 4.5), (12.1, 1.5), rad=-0.3)
save(fig, "fig_retegek.png")

# 6. Scanout -------------------------------------------------------------------
fig, ax = new(7.2, 3.9, 16, 9.6)
ax.add_patch(Rectangle((0.5, 0.8), 9.6, 5.4, fc="#1e1e1e", ec="black", lw=2))
for i in range(0, 28):
    y = 6.2 - i * 0.2
    ax.plot([0.5, 10.1], [y, y], color="#444444", lw=0.3)
ax.add_patch(Circle((3.6, 4.6), 0.33, fc="white", ec="white"))
ax.add_patch(Rectangle((9.25, 1.0), 0.75, 0.75, fc="black", ec="none")); ax.add_patch(Rectangle((9.42, 1.17), 0.4, 0.4, fc="white", ec="none"))
ax.plot([0.5, 10.1], [4.6, 4.6], color="#ffd54f", lw=1); ax.plot([0.5, 10.1], [1.37, 1.37], color="#ffd54f", lw=1)
ax.annotate("", xy=(10.6, 1.37), xytext=(10.6, 4.6), arrowprops=dict(arrowstyle="<->", color=RED, lw=1.3))
ax.text(10.8, 3.0, "Δy = y_inger − y_jelző\n(képpontsor; itt negatív)", fontsize=8, color=RED, va="center")
ax.annotate("", xy=(0.15, 0.8), xytext=(0.15, 6.2), arrowprops=dict(arrowstyle="-|>", color=GREY, lw=1.2))
ax.text(0.5, 0.3, "kirajzolás iránya: fentről lefelé, soronként", fontsize=8, color=GREY)
ax.text(3.6, 5.2, "inger (középső sora)", color="white", fontsize=7.5, ha="center")
ax.text(8.9, 1.37, "fotodióda-jelző", color="white", fontsize=7.5, ha="right", va="center")
# idotengely
ax.plot([0.5, 15.5], [8.2, 8.2], color=GREY, lw=1)
for x, t, c in [(1.0, "V-sync", GREY), (5.2, "inger sora", ORANGE), (12.0, "jelző sora\n(t_fény)", BLUE), (13.8, "holtidő", GREY)]:
    ax.plot([x, x], [8.05, 8.35], color=c, lw=1.6); ax.text(x, 8.5, t, ha="center", va="bottom", fontsize=7.5, color=c)
ax.annotate("", xy=(5.2, 7.85), xytext=(12.0, 7.85), arrowprops=dict(arrowstyle="-|>", color=RED, lw=1.2))
ax.text(8.6, 7.4, "korrekció = Δy · (aktív sorok / asztalmagasság) · (képidő / összes sor)", ha="center", fontsize=7.5, color=RED)
ax.text(15.6, 8.2, "t", fontsize=9, va="center")
save(fig, "fig_scanout.png")

# 7. Mérőfelület és tekintetszabályok ----------------------------------------
fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.6))
W, H = 1920, 1080
for k, ax in enumerate(axs):
    ax.add_patch(Rectangle((0, 0), W, H, fc="#1e1e1e", ec="black", lw=1.5))
    ax.add_patch(Rectangle((36, 36), W - 72, H - 72, fc="none", ec="#777777", lw=0.6, ls=":"))
    mx, my = 1844, 1004
    ax.add_patch(Rectangle((mx - 16, my - 16), 88, 88, fc="black", ec="none")); ax.add_patch(Rectangle((mx, my), 56, 56, fc="white", ec="none"))
    ax.add_patch(Rectangle((mx - 36, my - 36), 128, 128, fc="none", ec=RED, lw=0.6, ls="--"))
    cx, cy = W / 2, H / 2
    ax.plot([cx - 22, cx + 22], [cy, cy], color="white", lw=1.6); ax.plot([cx, cx], [cy - 22, cy + 22], color="white", lw=1.6)
    for r in (150, 500):
        ax.add_patch(Circle((cx, cy), r, fc="none", ec="#bbbbbb", lw=0.7, ls="--"))
    sx, sy = cx + 330 * math.cos(math.radians(-25)), cy + 330 * math.sin(math.radians(-25))
    ax.add_patch(Circle((sx, sy), 30, fc="white", ec="white"))
    if k == 0:
        ax.add_patch(Circle((sx, sy), 110, fc="none", ec="#ffa726", lw=1.3, ls="--")); ax.set_title("Normál mód: legalább egy minta az inger\nkörüli (d/2 + R) sugarú körben", fontsize=8)
    else:
        ax.add_patch(Circle((cx, cy), 80, fc="none", ec="#ffa726", lw=1.3, ls="--")); ax.set_title("Periférikus mód: minden minta a fixációs\npont körüli R sugarú körben", fontsize=8)
    ax.text(cx, cy - 175, "min.", color="#bbbbbb", fontsize=6, ha="center"); 
    ax.set_xlim(-10, W + 10); ax.set_ylim(H + 10, -10); ax.set_aspect("equal"); ax.axis("off")
fig.tight_layout()
save(fig, "fig_stage.png")

# 8. Előperiódus-eloszlás ------------------------------------------------------
fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.5))
mn, mxx, mu = 1000, 3000, 700
x = np.linspace(mn, mxx, 500)
trunc = 1 - np.exp(-(mxx - mn) / mu)
pdf = np.exp(-(x - mn) / mu) / (mu * trunc); cdf = (1 - np.exp(-(x - mn) / mu)) / trunc
updf = np.full_like(x, 1 / (mxx - mn)); ucdf = (x - mn) / (mxx - mn)
axs[0].plot(x, pdf * 1000, color=BLUE, label="csonkolt exponenciális (μ = 700 ms)"); axs[0].plot(x, updf * 1000, color=ORANGE, ls="--", label="egyenletes")
axs[0].set_xlabel("előperiódus [ms]"); axs[0].set_ylabel("sűrűség [1/s]"); axs[0].legend(fontsize=6.5, frameon=False); axs[0].set_title("Sűrűségfüggvény", fontsize=8)
hz_e = pdf / np.clip(1 - cdf, 1e-9, None); hz_u = updf / np.clip(1 - ucdf, 1e-9, None)
m = x < 2900
axs[1].plot(x[m], hz_e[m] * 1000, color=BLUE); axs[1].plot(x[m], hz_u[m] * 1000, color=ORANGE, ls="--")
axs[1].set_ylim(0, 8); axs[1].set_xlabel("eltelt várakozás [ms]"); axs[1].set_ylabel("feltételes ráta [1/s]"); axs[1].set_title("Az inger megjelenésének feltételes rátája", fontsize=8)
for a in axs:
    a.spines[["top", "right"]].set_visible(False); a.tick_params(labelsize=7)
fig.tight_layout()
save(fig, "fig_foreperiod.png")

# 9. Fejlesztés közbeni adatok (PC − ESP) ------------------------------------
D = r"C:\Users\mrenk\OneDrive\Dokumentumok\ReakcioidoMeres\measurements"
sess = ["20261109_183826", "20261109_183956", "20261109_185842", "20261109_190002"]
allrows = []
summ = []
for s in sess:
    f = [n for n in os.listdir(D) if n.startswith(s)][0]
    hdr = None; rows = []
    for line in open(os.path.join(D, f), encoding="utf-8-sig"):
        line = line.rstrip("\n")
        if line.startswith("#") or not line: continue
        if hdr is None: hdr = line.split(","); continue
        r = dict(zip(hdr, line.split(",")))
        if r["Phase"] == "Test" and r["Valid"] == "True" and r["EspReactionTimeMs"] and r["LocalReactionTimeMs"]:
            rows.append((float(r["EspReactionTimeMs"]), float(r["LocalReactionTimeMs"]), float(r["ScanoutCorrectionUs"]) / 1000))
    a = np.array(rows); d = a[:, 1] - a[:, 0]
    summ.append((s, len(a), a[:, 0].mean(), np.median(a[:, 0]), a[:, 1].mean(), d.mean(), np.median(d), d.std(ddof=1), d.min(), d.max()))
    allrows.append(a)
with open(os.path.join(OUT, "adatok_osszesites.txt"), "w", encoding="utf-8") as fo:
    for r in summ: fo.write(" | ".join(str(round(v, 1)) if not isinstance(v, str) else v for v in r) + "\n")
    A = np.vstack(allrows); d = A[:, 1] - A[:, 0]
    fo.write(f"ALL n={len(A)} esp_mean={A[:,0].mean():.1f} pc_mean={A[:,1].mean():.1f} d_mean={d.mean():.1f} d_med={np.median(d):.1f} d_sd={d.std(ddof=1):.1f} d_min={d.min():.1f} d_max={d.max():.1f} corr_scan={np.corrcoef(A[:,2], d)[0,1]:.2f} scan_min={A[:,2].min():.2f} scan_max={A[:,2].max():.2f}\n")
fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.7), gridspec_kw={"width_ratios": [1.5, 1]})
A = np.vstack(allrows); idx = np.arange(1, len(A) + 1)
axs[0].plot(idx, A[:, 1], "o-", color=RED, ms=3, lw=0.8, label="PC-idő (szoftveres)"); axs[0].plot(idx, A[:, 0], "s-", color=BLUE, ms=3, lw=0.8, label="ESP-idő (hardveres)")
c = 0
for a in allrows[:-1]:
    c += len(a); axs[0].axvline(c + 0.5, color=GREY, lw=0.5, ls=":")
axs[0].set_xlabel("érvényes mért próba sorszáma (4 mérés egymás után)"); axs[0].set_ylabel("reakcióidő [ms]"); axs[0].legend(fontsize=6.5, frameon=False)
d = A[:, 1] - A[:, 0]
axs[1].hist(d, bins=np.arange(25, 66, 5), color=LBLUE, ec=BLUE)
axs[1].axvline(d.mean(), color=RED, lw=1); axs[1].text(d.mean() + 0.8, axs[1].get_ylim()[1] * 0.92, f"átlag {d.mean():.1f} ms".replace(".", ","), fontsize=7, color=RED)
axs[1].set_xlabel("PC − ESP [ms]"); axs[1].set_ylabel("próbák száma")
for a in axs:
    a.spines[["top", "right"]].set_visible(False); a.tick_params(labelsize=7)
fig.tight_layout()
save(fig, "fig_adatok.png")

# 10. Elvi kapcsolások (TIA + gombbemenet) -------------------------------------
fig, ax = new(8.0, 3.0, 24, 8)
def wire(pts, c="black", lw=1.1):
    xs_, ys_ = zip(*pts); ax.plot(xs_, ys_, color=c, lw=lw, solid_capstyle="round")
def res(x, y, w=1.6, h=0.5, label=""):
    ax.add_patch(Rectangle((x, y - h / 2), w, h, fc="white", ec="black", lw=1.1)); ax.text(x + w / 2, y + 0.55, label, ha="center", fontsize=7.5)
def gnd(x, y):
    for i, ww in enumerate((0.5, 0.33, 0.16)):
        ax.plot([x - ww, x + ww], [y - i * 0.16, y - i * 0.16], color="black", lw=1.1)
# TIA
ax.add_patch(Polygon([(5.0, 2.2), (5.0, 5.0), (7.6, 3.6)], closed=True, fc="white", ec="black", lw=1.2))
ax.text(5.25, 4.4, "−", fontsize=10, va="center"); ax.text(5.25, 2.8, "+", fontsize=9, va="center"); ax.text(6.0, 3.6, "U2", fontsize=7.5, va="center")
wire([(2.0, 4.4), (5.0, 4.4)]); wire([(5.0, 2.8), (4.3, 2.8), (4.3, 1.6)]); gnd(4.3, 1.6)
# fotodioda
ax.add_patch(Polygon([(1.6, 3.1), (2.4, 3.1), (2.0, 3.7)], closed=True, fc="white", ec="black", lw=1.1)); ax.plot([1.6, 2.4], [3.7, 3.7], color="black", lw=1.1)
wire([(2.0, 3.7), (2.0, 4.4)]); wire([(2.0, 3.1), (2.0, 1.6)]); gnd(2.0, 1.6)
ax.text(1.1, 2.6, "BPW34", fontsize=7.5, ha="right")
for dy in (0, 0.35):
    ax.annotate("", xy=(1.5, 3.45 + dy), xytext=(0.7, 3.95 + dy), arrowprops=dict(arrowstyle="-|>", color=ORANGE, lw=0.9))
wire([(3.6, 4.4), (3.6, 6.2), (4.6, 6.2)]); res(4.6, 6.2, label="R_f"); wire([(6.2, 6.2), (8.4, 6.2), (8.4, 3.6)])
wire([(3.6, 6.2), (3.6, 7.4), (5.2, 7.4)]); ax.plot([5.2, 5.2], [7.1, 7.7], color="black", lw=1.3); ax.plot([5.6, 5.6], [7.1, 7.7], color="black", lw=1.3)
wire([(5.6, 7.4), (8.4, 7.4), (8.4, 6.2)]); ax.text(5.4, 7.85, "C_f", ha="center", fontsize=7.5)
wire([(7.6, 3.6), (9.6, 3.6)]); ax.text(9.7, 3.6, "→ ESP GPIO4\n(ADC1, 3-as csat.)", fontsize=7.5, va="center")
ax.text(5.6, 0.55, "U_ki = I_fotó · R_f      τ = R_f · C_f", ha="center", fontsize=8.5, color=BLUE)
ax.text(5.6, -0.2, "a) fotodióda transzimpedancia-erősítővel (elvi rajz)", ha="center", fontsize=8)
# gomb
ox = 17.5
wire([(ox + 1.0, 7.2), (ox + 1.0, 6.6)]); ax.text(ox + 1.0, 7.4, "3,3 V", ha="center", fontsize=7.5)
ax.add_patch(Rectangle((ox + 0.75, 5.0), 0.5, 1.6, fc="white", ec="black", lw=1.1)); ax.text(ox + 1.45, 5.8, "belső felhúzó", fontsize=7, va="center")
wire([(ox + 1.0, 5.0), (ox + 1.0, 4.2)]); wire([(ox - 1.2, 4.2), (ox + 3.2, 4.2)]); ax.text(ox + 3.3, 4.2, "→ ESP GPIO5\n(megszakítás:\nlefutó él)", fontsize=7.5, va="center")
wire([(ox - 1.2, 4.2), (ox - 1.2, 3.4)]); ax.plot([ox - 1.2, ox - 0.75], [3.4, 2.5], color="black", lw=1.2); wire([(ox - 1.2, 2.4), (ox - 1.2, 1.6)]); gnd(ox - 1.2, 1.6)
ax.plot(ox - 1.2, 3.4, "o", color="black", ms=3); ax.plot(ox - 1.2, 2.4, "o", color="black", ms=3)
ax.text(ox - 1.5, 2.9, "egér bal gomb\n(mikrokapcsoló /\nillesztő áramkör)", fontsize=7, ha="right", va="center")
ax.text(ox + 1.0, -0.2, "b) a válaszgomb bemenete (elvi rajz)", ha="center", fontsize=8)
ax.set_ylim(-0.6, 8.2)
save(fig, "fig_kapcsolas.png")

# 11. NYÁK-rajz a PDF-ből ------------------------------------------------------
import pypdfium2 as pdfium
pdf = pdfium.PdfDocument(r"C:\Users\mrenk\OneDrive\Dokumentumok\Egyéni Office-sablonok\TMDK\PCB_PCB_reakciomero_2026-11-14.pdf")
im = pdf[0].render(scale=6).to_pil().convert("RGB")
g = np.array(im.convert("L")); ys, xs_ = np.where(g < 200)
im = im.crop((xs_.min() - 60, ys.min() - 60, xs_.max() + 60, ys.max() + 60)); im.save(os.path.join(OUT, "fig_pcb.png")); print("pcb", im.size)

# 12. 3D modellek összeállított képei -----------------------------------------
def font(sz):
    for p in (r"C:\Windows\Fonts\arial.ttf", r"C:\Windows\Fonts\segoeui.ttf"):
        if os.path.exists(p): return ImageFont.truetype(p, sz)
    return ImageFont.load_default()
def sheet(items, cols, cell, name):
    rows_ = math.ceil(len(items) / cols)
    sh = Image.new("RGB", (cols * cell, rows_ * (cell + 46)), "white"); d = ImageDraw.Draw(sh); f = font(22)
    for i, (fn, lab) in enumerate(items):
        im_ = Image.open(os.path.join(SRC, fn)).convert("RGB"); im_.thumbnail((cell - 30, cell - 20))
        x = (i % cols) * cell; y = (i // cols) * (cell + 46)
        sh.paste(im_, (x + (cell - im_.width) // 2, y + (cell - im_.height) // 2))
        tw = d.textlength(lab, font=f); d.text((x + (cell - tw) / 2, y + cell + 6), lab, fill="black", font=f)
    sh.save(os.path.join(OUT, name)); print("ok", name, sh.size)
sheet([("r_35-60mm_Hook_U_BPW_Box_V13_0.png", "a) befogókengyel az érzékelőházzal"), ("r_35-60mm_Hook_U_BPW_Box_V13_1.png", "b) a kengyel a másik oldalról"),
       ("r_BPW_Head_V2_Final_1.png", "c) érzékelőfej (maszk a nyílással)"), ("r_Screw_35-60mm_plus_10mm_0.png", "d) szorítócsavar gömbfejjel"),
       ("r_Cap_0.png", "e) nyomótalp a csavar végére")], 3, 520, "fig_3d_szenzor.png")
sheet([("r_esp_doboz_v6_0.png", "a) dobozalj"), ("r_esp_doboz_v6_1.png", "b) dobozalj az USB-nyílás felől"), ("r_ESP_doboz_teto_v6_0.png", "c) fedél"),
       ("r_esp_doboz_usb_huvely_0.png", "d) USB-hüvely"), ("r_zaroklip_v4_0.png", "e) záróklip")], 3, 520, "fig_3d_doboz.png")

# 13. Fotók a TMDK-dolgozatból ---------------------------------------------------
for src, dst in [("tmdk_img_p22_Image411.jpg", "foto_esp32s3.png"), ("tmdk_img_p23_Image424.jpg", "foto_bpw34.png"), ("tmdk_img_p29_Image448.jp2", "foto_tobii.png")]:
    try:
        im_ = Image.open(os.path.join(SRC, src))
        if im_.mode == "RGBA":
            bg = Image.new("RGB", im_.size, "white"); bg.paste(im_, mask=im_.split()[3]); im_ = bg
        im_.convert("RGB").save(os.path.join(OUT, dst)); print("ok", dst)
    except Exception as e:
        print("ERR", src, e)
