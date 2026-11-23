# -*- coding: utf-8 -*-
"""纸/墨后处理：把 Affinity 画的骨架素材（按钮基底 / 标题纸板 / 双线框）过一遍
纸张颗粒与干墨，保持「中性白 + alpha」（颜色仍由 UITheme token 给）。
输入：桌面 reef-uiart\\ 的 Affinity 导出    输出：Assets\\Resources\\UIArt\\（同名同尺寸，.meta 不动）
对照图：Tools\\.uiart\\chrome-pass.png
用法：python Tools\\uiart-chromepass.py
"""
import os
import shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AFF = r"C:\Users\ZAIza\OneDrive\Desktop\reef-uiart"
DST = r"Assets\Resources\UIArt"
CACHE = r"Tools\.uiart\affinity"
BACKUP = os.path.join(CACHE, "pil-backup")
OUT = r"Tools\.uiart\chrome-pass.png"
FONT = r"Assets\Fonts\NotoSerifSC-VariableFont_wght.ttf"

# 名称, seed, 颗粒强度, 干墨强度, 9-slice 边, 染色 RGB, 染色 alpha, 实况尺寸
# 颗粒强度取「极轻」：九宫拉伸会把中区噪声沿拉伸方向抹成条带，幅度 >5% 就会看出来
JOBS = [
    ("btn_plate_9s",      7101, 0.035, 0.00, (12, 12, 12, 12), (240, 233, 214), 1.00, (150, 38)),
    ("titleplate_9s",     7102, 0.030, 0.00, (20, 0, 20, 0),   (240, 233, 214), 1.00, (260, 30)),
    ("rule_ring_9s",      7103, 0.030, 0.30, (8, 8, 8, 8),     (201, 191, 166), 0.90, (240, 120)),
    ("rule_ring_worn_9s", 7104, 0.030, 0.55, (8, 8, 8, 8),     (42, 35, 24),    0.55, (240, 120)),
]


def blur(a, s):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(s))).astype(np.float32) / 255.0


def paper_pass(rgba, seed, grain, dry, plate_like):
    """颗粒（均匀，9-slice 拉伸安全）+ 干墨（只作用于已有墨迹处，绝不新增中区不透明）。"""
    rng = np.random.default_rng(seed)
    a = rgba[:, :, 3].astype(np.float32) / 255.0
    h, w = a.shape
    out = rgba.copy()

    if grain > 0:
        n = blur(rng.random((h, w)).astype(np.float32), 0.55)      # 细颗粒
        n = 1.0 - grain * (1.0 - n)                                # 1-g .. 1
        a = a * n
        # RGB 起伏必须**对称**（均值 1.0），否则整块素材会整体偏离 token 颜色
        rgb = rgba[:, :, :3].astype(np.float32) * (1.0 + (n - 0.5) * 0.024)[:, :, None]
        out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)

    if dry > 0:
        # 干墨必须是**低频**变化：高频斑点在九宫拉伸后会变成条纹
        low = rng.random((max(2, h // 6), max(2, w // 6))).astype(np.float32)
        low = np.asarray(Image.fromarray((low * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
                         ).astype(np.float32) / 255.0
        edge = blur(a, 0.8)                                        # 墨迹覆盖场：只在有线的地方做干墨
        mask = np.clip(edge * 1.6, 0, 1)
        a = a * (1.0 - dry * mask * (1.0 - low))
        if not plate_like:                                         # 线环：随机掉几处小孔（缺墨）
            hole = np.clip(rng.random((h, w)).astype(np.float32) - 0.988, 0, None) / 0.012
            a = a * (1.0 - 0.6 * hole * mask)

    out[:, :, 3] = np.round(np.clip(a, 0, 1) * 255).astype(np.uint8)
    return out


def tinted(img, rgb, alpha):
    r, g, b, a = img.split()
    r = r.point(lambda v: int(v * rgb[0] / 255))
    g = g.point(lambda v: int(v * rgb[1] / 255))
    b = b.point(lambda v: int(v * rgb[2] / 255))
    a = a.point(lambda v: int(v * alpha))
    return Image.merge("RGBA", (r, g, b, a))


def slice9(img, border, w, h):
    l, t, r, b = border
    if l + r == 0 and t + b == 0:
        return img.resize((w, h), Image.NEAREST)
    iw, ih = img.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    midw, midh = w - l - r, h - t - b

    def put(box, size, pos):
        if size[0] > 0 and size[1] > 0:
            out.paste(img.crop(box).resize(size, Image.NEAREST), pos)

    put((0, 0, l, t), (l, t), (0, 0))
    put((iw - r, 0, iw, t), (r, t), (w - r, 0))
    put((0, ih - b, l, ih), (l, b), (0, h - b))
    put((iw - r, ih - b, iw, ih), (r, b), (w - r, h - b))
    put((l, 0, iw - r, t), (midw, t), (l, 0))
    put((l, ih - b, iw - r, ih), (midw, b), (l, h - b))
    put((0, t, l, ih - b), (l, midh), (0, t))
    put((iw - r, t, iw, ih - b), (r, midh), (w - r, t))
    put((l, t, iw - r, ih - b), (midw, midh), (l, t))
    return out


def stats(rgba):
    px = rgba.reshape(-1, 4)
    return "alpha 均 %.1f 峰 %d" % (px[:, 3].mean(), px[:, 3].max())


os.makedirs(BACKUP, exist_ok=True)
os.makedirs(CACHE, exist_ok=True)
font = ImageFont.truetype(FONT, 15)
small = ImageFont.truetype(FONT, 12)

rows = []
for (name, seed, grain, dry, border, rgb, alpha, (rw, rh)) in JOBS:
    src = os.path.join(AFF, name + ".png")
    if not os.path.exists(src):
        print("MISS " + src)
        continue
    dst = os.path.join(DST, name + ".png")
    if os.path.exists(dst) and not os.path.exists(os.path.join(BACKUP, name + ".png")):
        shutil.copy2(dst, os.path.join(BACKUP, name + ".png"))
    if not os.path.exists(os.path.join(CACHE, name + ".png")):
        shutil.copy2(src, os.path.join(CACHE, name + ".png"))
    raw = np.array(Image.open(src).convert("RGBA"))
    new = paper_pass(raw, seed, grain, dry, plate_like=("ring" not in name))
    img = Image.fromarray(new, "RGBA")
    img.save(dst)
    img.save(os.path.join(CACHE, name + "-chrome.png"))
    old = None
    p = os.path.join(BACKUP, name + ".png")
    if os.path.exists(p):
        old = Image.open(p).convert("RGBA")
    rows.append((name, border, rgb, alpha, rw, rh, old, Image.fromarray(raw, "RGBA"), img))
    print("%-20s %s -> %s" % (name, "Affinity " + stats(raw), "后处理 " + stats(new)))

# ── 对照图：每行 = 旧 PIL 实况 | 新（Affinity+后处理）实况 | 6x 放大角 ────────
PAD, BG, INK, Z = 14, (231, 222, 200), (42, 35, 24), 6
live_w = max(r[4] for r in rows)
zoom_w = 20 * Z
sheet_w = PAD * 4 + live_w * 2 + zoom_w * 2
sheet_h = 34 + sum(max(r[5], 24 * Z) + 26 for r in rows) + PAD * 3
sheet = Image.new("RGB", (sheet_w, sheet_h), BG)
d = ImageDraw.Draw(sheet)
d.text((PAD, 9), "每行：旧程序化(PIL)实况 | 新 Affinity+纸墨后处理 实况 || 上/下角放 6x 放大（看线口与断口）",
       fill=INK, font=font)

y = 34
for (name, border, rgb, alpha, rw, rh, old, raw_aff, new) in rows:
    d.text((PAD, y), "%s  border=%s  tint=%s a=%.2f %dx%d" % (name, border, rgb, alpha, rw, rh),
           fill=INK, font=small)
    y += 18
    x = PAD
    for im in (old, new):
        if im is None:
            x += live_w + PAD
            continue
        cell = Image.new("RGB", (rw, rh), BG)
        big = tinted(slice9(im, border, rw, rh), rgb, alpha)
        cell.paste(big, (0, 0), big)
        sheet.paste(cell, (x, y))
        x += live_w + PAD
    for im in (old, new):
        c = Image.new("RGB", (20 * Z, 24 * Z), (245, 242, 234))
        if im is not None:
            crop = im.crop((0, 0, min(20, im.width), min(24, im.height)))
            big = crop.resize((crop.width * Z, crop.height * Z), Image.NEAREST)
            c.paste(big, (0, 0), big)
        sheet.paste(c, (x, y))
        x += zoom_w + PAD
    y += max(rh, 24 * Z) + 8

sheet.save(OUT)
print("saved %s  %s" % (OUT, sheet.size))

# ── 追加：按钮五态实况（旧 vs 新）+ 左端 2x 放大，专门看 9-slice 角/中区接缝 ──
btn = [r for r in rows if r[0] == "btn_plate_9s"]
if btn:
    name, border, rgb, alpha, rw, rh, old, raw_aff, new = btn[0]
    states = [("normal", (240, 233, 214)), ("hover", (230, 218, 190)),
              ("press", (211, 200, 174)), ("disabled", (214, 205, 181))]
    W, H = 220, 56
    out2 = Image.new("RGB", (PAD * 3 + W * 2 + 60 * 4 + PAD * 4, 34 + len(states) * (H + PAD)), BG)
    d2 = ImageDraw.Draw(out2)
    d2.text((PAD, 9), "按钮基底：左=旧程序化 / 右=新 Affinity+纸墨（四种底色各渲一次，右侧为左端 2x 放大）",
            fill=INK, font=font)
    y = 30
    for label, tint in states:
        d2.text((PAD, y), label, fill=INK, font=small)
        x = PAD + 56
        for im in (old, new):
            if im is None:
                x += W + PAD
                continue
            cell = Image.new("RGB", (W, H), (231, 222, 200))
            big = tinted(slice9(im, border, W, H), tint, 1.0)
            cell.paste(big, (0, 0), big)
            out2.paste(cell, (x, y))
            x += W + PAD
        frame = 60
        for im in (old, new):
            c = Image.new("RGB", (frame, H * 2), (231, 222, 200))
            if im is not None:
                big = tinted(slice9(im, border, W, H), tint, 1.0).crop((0, 0, frame // 2, H))
                big = big.resize((big.width * 2, big.height * 2), Image.NEAREST)
                c.paste(big, (0, 0), big)
            out2.paste(c, (x, y))
            x += frame + PAD
        y += H + PAD
    p2 = r"Tools\.uiart\btn-states.png"
    out2.save(p2)
    print("saved %s  %s" % (p2, out2.size))
