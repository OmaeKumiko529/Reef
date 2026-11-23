# -*- coding: utf-8 -*-
"""
uiart-preview.py —— 「暗礁」UI 素材离线预览（Lead 唯一属主）

不需要 Unity：用 Pillow 按 UGUI 的规则（Image.Type.Sliced + Image.color 乘算）
把 Assets/Resources/UIArt/ 的贴图合成成面板 / 按钮 / 日志卡片，用来判断素材是否达标。

输出 Tools/.uiart/preview-sheet.png
用法：python Tools/uiart-preview.py
"""

import os
import sys
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Resources", "UIArt")
OUT = os.path.join(ROOT, "Tools", ".uiart")
FONT_PATH = os.path.join(ROOT, "Assets", "Fonts", "NotoSerifSC-VariableFont_wght.ttf")

# ── token 颜色（逐字取自 Assets/UI/UITheme.cs:6-20,44,115-146,154-171）─────────
PAPER_BG = (231, 222, 200)
PAPER_TOP = (240, 233, 214)
INK = (42, 35, 24)
INK2 = (90, 81, 69)
MUTED = (138, 128, 114)
SEAL = (160, 48, 24)
GOLD = (176, 138, 62)
DIV = (201, 191, 166)
REDACT = (58, 52, 44)
BACKDROP = (180, 173, 157)
CHROME_OUTER = (42, 35, 24, 140)     # ChromeOuterColor #2A2318 @0.55
CHROME_INNER = (201, 191, 166, 230)  # ChromeInnerColor #C9BFA6 @0.90
HIGHLIGHT = (240, 233, 214, 140)
PRESS = (42, 35, 24, 90)
TITLE_RULE = (42, 35, 24, 217)       # TitleRuleColor @0.85
DOSSIER_BG = (201, 191, 166, 115)    # DossierTagBg @0.45
BTN_HOVER_BG = (230, 218, 190)
BTN_PRESSED_BG = (211, 200, 174)
BTN_DISABLED_BG = (214, 205, 181)


def font(px, weight=600):
    f = ImageFont.truetype(FONT_PATH, px)
    try:
        f.set_variation_by_axes([weight])
    except Exception:
        pass
    return f


def load(name):
    return Image.open(os.path.join(ART, name + ".png")).convert("RGBA")


def tint(img, color):
    """UGUI Image.color 的等效：RGB 相乘、A 相乘（贴图本身是中性白/灰）。"""
    r, g, b, a = img.split()
    tr, tg, tb = color[0], color[1], color[2]
    ta = color[3] if len(color) > 3 else 255
    r = r.point(lambda v: v * tr // 255)
    g = g.point(lambda v: v * tg // 255)
    b = b.point(lambda v: v * tb // 255)
    a = a.point(lambda v: v * ta // 255)
    return Image.merge("RGBA", (r, g, b, a))


def nine_slice(img, border, w, h):
    """UGUI Image.Type.Sliced 的几何规则。"""
    l, b, r, t = border
    iw, ih = img.size
    if l + r >= w or t + b >= h:
        return img.resize((w, h), Image.LANCZOS)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    src = {"tl": (0, 0, l, t), "tc": (l, 0, iw - r, t), "tr": (iw - r, 0, iw, t),
           "ml": (0, t, l, ih - b), "mc": (l, t, iw - r, ih - b), "mr": (iw - r, t, iw, ih - b),
           "bl": (0, ih - b, l, ih), "bc": (l, ih - b, iw - r, ih), "br": (iw - r, ih - b, iw, ih)}
    dst = {"tl": (0, 0, l, t), "tc": (l, 0, w - r, t), "tr": (w - r, 0, w, t),
           "ml": (0, t, l, h - b), "mc": (l, t, w - r, h - b), "mr": (w - r, t, w, h - b),
           "bl": (0, h - b, l, h), "bc": (l, h - b, w - r, h), "br": (w - r, h - b, w, h)}
    for k in src:
        sx0, sy0, sx1, sy1 = src[k]
        dx0, dy0, dx1, dy1 = dst[k]
        tw, th = max(1, dx1 - dx0), max(1, dy1 - dy0)
        piece = img.crop((sx0, sy0, sx1, sy1))
        if piece.size != (tw, th):
            piece = piece.resize((tw, th), Image.LANCZOS)
        out.alpha_composite(piece, (dx0, dy0))
    return out


def blit(canvas, img, x, y):
    canvas.alpha_composite(img, (int(x), int(y)))


def rule(canvas, x, y, w, h, color, asset="rule_ring_9s", border=(8, 8, 8, 8)):
    blit(canvas, nine_slice(tint(load(asset), color), border, w, h), x, y)


# ── 一号面板：纸底 + 双线框 + 标题栏 + 朱砂竖块 + 标题下 2px 线 ────────────────
def panel(w, h, title=None, title_size=22, paper=PAPER_TOP, kind="panel", tag=None, stamp=None):
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    blit(c, nine_slice(tint(load("paper_plate_9s"), paper), (18, 18, 18, 18), w, h), 0, 0)
    if kind in ("panel", "modal"):
        tb = 56
        blit(c, nine_slice(tint(load("titleplate_9s"), PAPER_TOP), (20, 0, 20, 0), w, tb), 0, 0)
        d = ImageDraw.Draw(c)
        d.rectangle([12, (tb - 18) // 2, 12 + 3, (tb - 18) // 2 + 17], fill=SEAL + (255,))
        if title:
            d.text((20, (tb - title_size) // 2 - 3), title, font=font(title_size, 700), fill=INK + (255,))
        blit(c, nine_slice(tint(load("rule_ring_9s"), TITLE_RULE), (8, 8, 8, 8), w - 0, 2), 0, tb - 2)
    # 双线框：外 1px + 内 1px（内线在纸底外扩 3px 处）
    blit(c, nine_slice(tint(load("rule_ring_worn_9s"), CHROME_OUTER), (8, 8, 8, 8), w + 2, h + 2), -1, -1)
    blit(c, nine_slice(tint(load("rule_ring_9s"), CHROME_INNER), (8, 8, 8, 8), w + 6, h + 6), -3, -3)
    blit(c, nine_slice(tint(load("paper_plate_9s"), paper), (18, 18, 18, 18), w, h), 0, 0) if False else None
    if tag:
        d = ImageDraw.Draw(c)
        tw = 8 + len(tag) * 11 + 8
        blit(c, nine_slice(tint(load("dossier_plate_9s"), DOSSIER_BG), (12, 0, 12, 0), tw, 22), w - tw - 12, 56 + 12)
        d.text((w - tw - 12 + 8, 56 + 15), tag, font=font(14, 500), fill=INK2 + (255,))
    if stamp:
        s = load(stamp).rotate(8, resample=Image.BICUBIC, expand=True)
        blit(c, s, w - s.width - 18, h - s.height - 16)
    return c


def card(w, title, lines, redacted=False, tag=None):
    pad = 16
    body = len(lines) * (16 + 8) if redacted else len(lines) * 22 + 6
    h = pad * 2 + 26 + body
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    blit(c, nine_slice(tint(load("paper_plate_9s"), PAPER_TOP), (18, 18, 18, 18), w, h), 0, 0)
    d = ImageDraw.Draw(c)
    d.text((pad, pad - 2), title, font=font(20, 700), fill=INK + (255,))
    y = pad + 30
    if redacted:
        for seg in lines:
            bw = int((w - pad * 2) * seg)
            blit(c, nine_slice(tint(load("redaction_bar_9s"), REDACT), (12, 0, 12, 0), bw, 16), pad, y)
            y += 16 + 8
    else:
        for ln in lines:
            d.text((pad, y), ln, font=font(18, 400), fill=INK2 + (255,))
            y += 22
    if tag:
        tw = 8 + len(tag) * 10 + 8
        blit(c, nine_slice(tint(load("dossier_plate_9s"), DOSSIER_BG), (12, 0, 12, 0), tw, 18), w - tw - 10, 10)
        d.text((w - tw - 2, 13), tag, font=font(12, 500), fill=INK2 + (255,))
    blit(c, nine_slice(tint(load("rule_ring_9s"), DIV + (230,)), (8, 8, 8, 8), w - 1, h - 1), 0, 0)
    return c


def button(state, w, h, label):
    bg = {"normal": PAPER_TOP, "hover": BTN_HOVER_BG, "press": BTN_PRESSED_BG,
          "selected": PAPER_TOP, "disabled": BTN_DISABLED_BG}[state]
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    blit(c, nine_slice(tint(load("btn_plate_9s"), bg), (12, 12, 12, 12), w, h), 0, 0)
    d = ImageDraw.Draw(c)
    col = MUTED if state == "disabled" else INK
    bbox = d.textbbox((0, 0), label, font=font(20, 600))
    d.text(((w - (bbox[2] - bbox[0])) / 2, (h - (bbox[3] - bbox[1])) / 2 - bbox[1]),
           label, font=font(20, 600), fill=col + (255,))
    if state == "selected":
        d.rectangle([0, 0, 2, h - 1], fill=SEAL + (255,))
    if state == "hover":
        blit(c, nine_slice(tint(load("rule_ring_9s"), GOLD + (255,)), (8, 8, 8, 8), w - 1, h - 1), 0, 0)
    if state == "disabled":
        blit(c, nine_slice(tint(load("rule_ring_9s"), DIV + (230,)), (8, 8, 8, 8), w - 1, h - 1), 0, 0)
    return c


def zoom(img, box, scale):
    return img.crop(box).resize(((box[2] - box[0]) * scale, (box[3] - box[1]) * scale), Image.NEAREST)


def main():
    os.makedirs(OUT, exist_ok=True)
    W, H = 1240, 1000
    sheet = Image.new("RGBA", (W, H), BACKDROP + (255,))
    d = ImageDraw.Draw(sheet)
    F = lambda px, w=600: font(px, w)

    # 1. 面板 + 五态按钮 ─────────────────────────────────────────────────────
    d.text((24, 14), "1. panel chrome / dossier tag / stamp  +  button five states", font=F(18, 700), fill=INK + (255,))
    p = panel(620, 420, "机构总览 · 渗透处", tag="档号 渗-01", stamp="stamp_weikaifang")
    blit(sheet, p, 24, 44)
    d.text((48, 158), "等级 2 ｜ 覆盖 3 省 ｜ 日 +1.5 ｜ 上限 +2", font=F(18, 400), fill=INK2 + (255,))
    for i, row in enumerate(["省情档案", "渗透名单", "因果链", "卷宗 03", "未见记载"]):
        y = 200 + i * 46
        d.text((48, y), row, font=F(20, 400), fill=INK + (255,))
        d.line([(48, y + 32), (628, y + 32)], fill=DIV + (170,), width=1)
    bx = 676
    for i, (st, lbl) in enumerate([("normal", "接见代表"), ("hover", "主持谈判"), ("press", "按下"),
                                   ("selected", "当前：渗透处"), ("disabled", "未开放")]):
        blit(sheet, button(st, 240, 40, lbl), bx, 48 + i * 52)
        d.text((bx + 252, 58 + i * 52), st, font=F(16, 500), fill=INK + (255,))
    # 事件选项行（带批示前缀）
    d.text((676, 318), "2. event option row (button + hints)", font=F(18, 700), fill=INK + (255,))
    ev = panel(520, 132, None)
    d = ImageDraw.Draw(ev)
    d.text((20, 14), "首都请愿", font=F(22, 700), fill=INK + (255,))
    d.line([(20, 46), (500, 46)], fill=DIV + (200,), width=1)
    blit(ev, button("normal", 480, 40, "批示 · 接见请愿代表，公开回应诉求"), 20, 58)
    blit(ev, button("disabled", 480, 32, "批示 · 令警察总署清场"), 20, 96)
    blit(sheet, ev, 676, 344)

    # 3. 日志卡片墙 ─────────────────────────────────────────────────────────
    d.text((24, 490), "3. journal = dossier wall (through-line + binding dots + redaction)", font=F(18, 700), fill=INK + (255,))
    x = 24
    y = 520
    cards = [card(380, "卷宗 01 · 人民之春", ["请愿从面包价格开始，止于议会门口。", "那天没有响起的汽笛。"], tag="已揭示"),
             card(380, "卷宗 02 · 因果迷雾", [0.78, 0.52, 0.66], True, tag="未揭示"),
             card(380, "卷宗 03 · 因果迷雾", [0.58, 0.72], True, tag="未揭示")]
    ys = []
    for c in cards:
        blit(sheet, c, x, y)
        ys.append(y + c.height // 2)
        y += c.height + 10
    dot = tint(load("binding_dot"), (138, 128, 114, 235))
    for i in range(len(ys) - 1):
        d.line([(x + 9, ys[i] + 4), (x + 9, ys[i + 1] + 4)], fill=DIV + (235,), width=1)
    for yy in ys:
        blit(sheet, dot, x + 6, yy)
    st = load("stamp_jimi").rotate(8, resample=Image.BICUBIC, expand=True)
    blit(sheet, st, x + 236, y - 108)

    # 4. 细节放大 ───────────────────────────────────────────────────────────
    zx, zy = 440, 490
    d.text((zx, zy - 4), "4. detail zoom x4", font=F(18, 700), fill=INK + (255,))
    blit(sheet, zoom(load("rule_ring_worn_9s"), (0, 0, 16, 16), 6), zx, zy + 26)
    d.text((zx, zy + 130), "outer ring corner (worn)", font=F(14, 500), fill=INK + (255,))
    blit(sheet, zoom(load("titleplate_9s"), (0, 40, 40, 56), 4), zx + 120, zy + 26)
    d.text((zx + 120, zy + 100), "titleplate bottom 3px", font=F(14, 500), fill=INK + (255,))
    blit(sheet, zoom(load("btn_plate_9s"), (0, 0, 20, 20), 5), zx + 300, zy + 26)
    d.text((zx + 300, zy + 130), "button plate corner", font=F(14, 500), fill=INK + (255,))
    blit(sheet, zoom(tint(load("binding_dot"), (138, 128, 114, 235)), (0, 0, 8, 8), 12), zx + 420, zy + 26)
    d.text((zx + 420, zy + 130), "binding dot x12", font=F(14, 500), fill=INK + (255,))
    blit(sheet, zoom(load("paper_plate_9s"), (0, 0, 24, 24), 5), zx, zy + 160)
    d.text((zx, zy + 295), "paper edge aging", font=F(14, 500), fill=INK + (255,))
    ov = Image.new("RGBA", (150, 96), PAPER_TOP + (255,))
    ov.alpha_composite(load("grain_tile").crop((0, 0, 150, 96)))
    ht = load("halftone_tile")
    for ox in range(0, 150, 64):
        for oy in range(0, 96, 64):
            ov.alpha_composite(ht, (ox, oy))
    blit(sheet, ov, zx + 140, zy + 160)
    d.text((zx + 140, zy + 262), "grain + halftone", font=F(14, 500), fill=INK + (255,))
    vg = Image.new("RGBA", (150, 96), BACKDROP + (255,))
    vg.alpha_composite(load("vignette").resize((150, 96), Image.LANCZOS))
    blit(sheet, vg, zx + 310, zy + 160)
    d.text((zx + 310, zy + 262), "vignette over map backdrop", font=F(14, 500), fill=INK + (255,))
    rb = Image.new("RGBA", (150, 96), PAPER_TOP + (255,))
    rb.alpha_composite(nine_slice(tint(load("redaction_bar_9s"), REDACT), (12, 0, 12, 0), 120, 16), (14, 40))
    blit(sheet, rb, zx + 480, zy + 160)
    d.text((zx + 480, zy + 262), "redaction bar", font=F(14, 500), fill=INK + (255,))

    d.text((24, 960), "generated offline from UITheme tokens | tinting simulated: rgb*token/255",
           font=F(15, 500), fill=INK + (255,))
    out = os.path.join(OUT, "preview-sheet.png")
    sheet.convert("RGB").save(out, "PNG")
    print("preview -> %s  (%dx%d)" % (out, W, H))
    return 0


if __name__ == "__main__":
    sys.exit(main())
