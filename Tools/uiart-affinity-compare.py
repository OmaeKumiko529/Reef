# -*- coding: utf-8 -*-
"""Affinity 素材 vs 现有 PIL 素材：既能看原图，也能看「按 UI 用法染色 + 九宫拉伸」后的实况。
用法：python Tools\\uiart-affinity-compare.py
输出：Tools\\.uiart\\affinity-compare.png
"""
import os
from PIL import Image, ImageDraw, ImageFont

AFF = r"Tools\.uiart\affinity"
PROJ = r"Assets\Resources\UIArt"
OUT = r"Tools\.uiart\affinity-compare.png"
FONT = r"Assets\Fonts\NotoSerifSC-VariableFont_wght.ttf"

PAPER = (231, 222, 200)          # PaperBg #E7DEC8 面板底
INK = (42, 35, 24)
SEALRED = (160, 48, 24)

# 名称: (九宫 border, 染色 RGB/alpha, 实况渲染尺寸)
SPEC = [
    ("stamp_weikaifang", (0, 0, 0, 0), ((255, 255, 255), 1.0), (176, 72)),
    ("stamp_shentou",    (0, 0, 0, 0), ((255, 255, 255), 1.0), (176, 72)),
    ("stamp_jimi",       (0, 0, 0, 0), ((255, 255, 255), 1.0), (128, 128)),
    ("rule_ring_9s",     (8, 8, 8, 8), ((201, 191, 166), 0.90), (240, 120)),
    ("rule_ring_worn_9s",(8, 8, 8, 8), ((42, 35, 24), 0.55), (240, 120)),
    ("titleplate_9s",    (20, 0, 20, 0), ((240, 233, 214), 1.0), (260, 30)),
    ("btn_plate_9s",     (12, 12, 12, 12), ((240, 233, 214), 1.0), (150, 38)),
    ("dossier_plate_9s", (12, 0, 12, 0), ((201, 191, 166), 0.45), (96, 22)),
    ("redaction_bar_9s", (12, 0, 12, 0), ((58, 52, 44), 1.0), (180, 16)),
]

SCALE = 3
PAD = 16
font = ImageFont.truetype(FONT, 15)
small = ImageFont.truetype(FONT, 12)


def checker(w, h, cell=8):
    im = Image.new("RGB", (w, h), (245, 242, 234))
    d = ImageDraw.Draw(im)
    for y in range(0, h, cell):
        for x in range(0, w, cell):
            if ((x // cell) + (y // cell)) % 2:
                d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(214, 208, 194))
    return im


def load(path):
    return Image.open(path).convert("RGBA") if os.path.exists(path) else None


def tinted(img, rgb, alpha):
    """按 UGUI 的方式：RGB 相乘、alpha 相乘。"""
    r, g, b, a = img.split()
    r = r.point(lambda v: int(v * rgb[0] / 255))
    g = g.point(lambda v: int(v * rgb[1] / 255))
    b = b.point(lambda v: int(v * rgb[2] / 255))
    a = a.point(lambda v: int(v * alpha))
    return Image.merge("RGBA", (r, g, b, a))


def slice9(img, border, w, h):
    """Unity Image.Type.Sliced 的近似：四角原样、四边单向拉伸、中区双向拉伸。"""
    l, t, r, b = border
    if l + r == 0 and t + b == 0:
        return img.resize((w, h), Image.NEAREST)
    iw, ih = img.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    midw, midh = w - l - r, h - t - b

    def put(box, size, pos):
        if size[0] <= 0 or size[1] <= 0:
            return
        out.paste(img.crop(box).resize(size, Image.NEAREST), pos)

    # 四角
    put((0, 0, l, t), (l, t), (0, 0))
    put((iw - r, 0, iw, t), (r, t), (w - r, 0))
    put((0, ih - b, l, ih), (l, b), (0, h - b))
    put((iw - r, ih - b, iw, ih), (r, b), (w - r, h - b))
    # 四边
    put((l, 0, iw - r, t), (midw, t), (l, 0))
    put((l, ih - b, iw - r, ih), (midw, b), (l, h - b))
    put((0, t, l, ih - b), (l, midh), (0, t))
    put((iw - r, t, iw, ih - b), (r, midh), (w - r, t))
    # 中区
    put((l, t, iw - r, ih - b), (midw, midh), (l, t))
    return out


def on_bg(img, w, h, bg=PAPER, scale=1):
    base = Image.new("RGB", (w * scale, h * scale), bg)
    if img is not None:
        big = img.resize((w * scale, h * scale), Image.NEAREST)
        base.paste(big, (0, 0), big)
    return base


rows = []
for (name, border, (rgb, alpha), (rw, rh)) in SPEC:
    p = load(os.path.join(PROJ, name + ".png"))
    a = load(os.path.join(AFF, name + ".png"))
    if a is None:
        continue
    raw_w = max(p.width if p else 0, a.width) * SCALE
    raw_h = max(p.height if p else 0, a.height) * SCALE
    rows.append((name, border, (rgb, alpha), rw, rh, p, a, raw_w, raw_h))

raw_col = max(r[7] for r in rows)
live_col = max(r[3] for r in rows)
col_w = max(raw_col, live_col)
sheet_w = PAD * 3 + col_w * 2
sheet_h = 34 + sum(max(r[8], r[4]) + 30 + PAD for r in rows) + PAD
sheet = Image.new("RGB", (sheet_w, sheet_h), PAPER)
d = ImageDraw.Draw(sheet)

d.text((PAD, 9), "左列 = 现有 PIL 生成 / 右列 = Affinity 原生绘制   上=原图(3x,棋盘底)  下=按 UI 用法染色+九宫拉伸后的实况",
       fill=INK, font=font)
y = 34
for (name, border, (rgb, alpha), rw, rh, p, a, raw_w, raw_h) in rows:
    d.text((PAD, y), "%s   border=%s   tint=rgb%s a=%.2f   → %dx%d" % (name, border, rgb, alpha, rw, rh),
           fill=INK, font=small)
    y += 20
    # 原图
    left = checker(raw_w, raw_h)
    if p:
        left.paste(p.resize((p.width * SCALE, p.height * SCALE), Image.NEAREST),
                   ((raw_w - p.width * SCALE) // 2, (raw_h - p.height * SCALE) // 2),
                   p.resize((p.width * SCALE, p.height * SCALE), Image.NEAREST))
    right = checker(raw_w, raw_h)
    right.paste(a.resize((a.width * SCALE, a.height * SCALE), Image.NEAREST),
                ((raw_w - a.width * SCALE) // 2, (raw_h - a.height * SCALE) // 2),
                a.resize((a.width * SCALE, a.height * SCALE), Image.NEAREST))
    sheet.paste(left, (PAD, y))
    sheet.paste(right, (PAD * 2 + col_w, y))
    y += raw_h + 4
    # 实况
    if p:
        sheet.paste(on_bg(tinted(slice9(p, border, rw, rh), rgb, alpha), rw, rh), (PAD, y))
    else:
        d.text((PAD + 4, y + 4), "(无)", fill=INK, font=small)
    sheet.paste(on_bg(tinted(slice9(a, border, rw, rh), rgb, alpha), rw, rh), (PAD * 2 + col_w, y))
    y += rh * 1 + PAD + 10

sheet.save(OUT)
print("saved %s  %s" % (OUT, sheet.size))

# 同时打印量化对照（RGB 均值 / alpha 均值 / 峰值）
for (name, border, (rgb, alpha), rw, rh, p, a, _, _) in rows:
    def stat(img):
        if img is None:
            return "-"
        px = list(img.convert("RGBA").getdata())
        n = len(px)
        mr = sum(q[0] for q in px) // n
        ma = sum(q[3] for q in px) / n
        pa = max(q[3] for q in px)
        return "rgb~%3d alpha mean %.3f max %d" % (mr, ma, pa)
    print("%-20s PIL %-38s AFF %s" % (name, stat(p), stat(a)))
