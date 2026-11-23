# -*- coding: utf-8 -*-
"""朱砂印泥后处理：把 Affinity 画的「干净矢量印章」过一遍干湿不匀的印泥效果。
输入：Affinity 导出的 PNG（桌面 reef-uiart\\）
输出：项目素材 Assets\\Resources\\UIArt\\stamp_*.png（同尺寸、同文件名，.meta 不受影响）
      + 对照图 Tools\\.uiart\\stamp-inkpass.png
用法：python Tools\\uiart-inkpass.py
"""
import os
import shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AFF = r"C:\Users\ZAIza\OneDrive\Desktop\reef-uiart"
DST = r"Assets\Resources\UIArt"
CACHE = r"Tools\.uiart\affinity"
BACKUP = r"Tools\.uiart\affinity\pil-backup"
OUT = r"Tools\.uiart\stamp-inkpass.png"
FONT = r"Assets\Fonts\NotoSerifSC-VariableFont_wght.ttf"

# (名字, 随机种子, 总墨量系数)
JOBS = [("stamp_weikaifang", 20261016, 0.92),
        ("stamp_shentou", 20261017, 0.92),
        ("stamp_jimi", 20261018, 0.92)]


def blur(a, s):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(s))).astype(np.float32) / 255.0


def ink_pass(rgba, seed, amount):
    """rgba: HxWx4 uint8 → 印泥化后的同尺寸数组。"""
    rng = np.random.default_rng(seed)
    a = rgba[:, :, 3].astype(np.float32) / 255.0
    h, w = a.shape
    cov = blur(a, 1.1)                                   # 墨迹覆盖场

    # 1) 墨色颗粒（细噪点）
    grain = 0.78 + 0.44 * blur(rng.random((h, w)).astype(np.float32), 0.55)
    # 2) 干擦条纹（低频，方向偏竖向，模拟印泥没蘸匀）
    sh, sw = max(2, h // 4), max(2, w // 6)
    low = rng.random((sh, sw)).astype(np.float32)
    low = np.asarray(Image.fromarray((low * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
                     ).astype(np.float32) / 255.0
    streak = np.clip(1.0 - 0.80 * np.clip(low - 0.40, 0, None) / 0.60, 0.30, 1.0)
    # 3) 边缘啃噬（越靠边越容易被纸纤维吃掉）
    edge = np.clip((0.55 - cov) / 0.55, 0, 1)
    bite = 1.0 - 0.75 * edge * np.clip(rng.random((h, w)).astype(np.float32) + 0.20, 0, 1)
    # 4) 缺墨小孔（线内随机掉点）
    hole = np.clip(rng.random((h, w)).astype(np.float32) - 0.965, 0, None) / 0.035
    body = 1.0 - 0.55 * hole * np.clip(cov * 1.4, 0, 1)

    alpha = a * grain * streak * bite * body
    # 5) 洇墨：轻微外扩
    alpha = np.maximum(alpha, blur(a, 1.5) * 0.28)
    # 6) 二次落印（整体错位一点点的浅影）
    ghost = np.roll(np.roll(a, 2, axis=0), 1, axis=1) * 0.15
    alpha = np.clip(np.maximum(alpha, ghost), 0, 1) * amount

    out = rgba.copy()
    out[:, :, 3] = np.round(alpha * 255).astype(np.uint8)
    return out


def stats(rgba):
    px = rgba.reshape(-1, 4)
    return "重 %.0f%%  半透 %.0f%%  均 alpha %.1f  峰 %d" % (
        100.0 * (px[:, 3] > 200).mean(), 100.0 * ((px[:, 3] > 20) & (px[:, 3] <= 200)).mean(),
        px[:, 3].mean(), px[:, 3].max())


os.makedirs(BACKUP, exist_ok=True)
os.makedirs(CACHE, exist_ok=True)
font = ImageFont.truetype(FONT, 15)
small = ImageFont.truetype(FONT, 12)

pairs = []
for name, seed, amount in JOBS:
    src = os.path.join(AFF, name + ".png")
    if not os.path.exists(src):
        print("MISS " + src)
        continue
    dst = os.path.join(DST, name + ".png")
    if os.path.exists(dst) and not os.path.exists(os.path.join(BACKUP, name + ".png")):
        shutil.copy2(dst, os.path.join(BACKUP, name + ".png"))      # 原 PIL 版留底，便于回退
    before = np.array(Image.open(src).convert("RGBA"))
    after = ink_pass(before, seed, amount)
    img = Image.fromarray(after, "RGBA")
    img.save(dst)
    img.save(os.path.join(CACHE, name + "-ink.png"))
    pairs.append((name, Image.fromarray(before, "RGBA"), img))
    print("%-20s %dx%d  Affinity %s | 印泥后 %s" % (name, img.width, img.height,
                                                    stats(before), stats(after)))

# 对照图：上排 Affinity 原样，下排印泥后
SC, PAD, BG, INK = 3, 12, (231, 222, 200), (42, 35, 24)
cells = [(im.width * SC, im.height * SC) for _, _, im in pairs]
row_h = max(h for _, h in cells)
sheet_w = PAD + sum(w for w, _ in cells) + PAD * (len(pairs) - 1) + PAD
sheet = Image.new("RGB", (sheet_w, 40 + (row_h + 34) * 2 + PAD), BG)
d = ImageDraw.Draw(sheet)
d.text((PAD, 8), "上：Affinity 直接导出（干净矢量）    下：过一遍朱砂印泥后处理（入项目）", fill=INK, font=font)
y = 36
for label, idx in (("Affinity 原样", 1), ("印泥化（已入 Assets/Resources/UIArt）", 2)):
    d.text((PAD, y), label, fill=INK, font=small)
    y += 20
    x = PAD
    for (name, before, after) in pairs:
        im = before if idx == 1 else after
        big = im.resize((im.width * SC, im.height * SC), Image.NEAREST)
        sheet.paste(big, (x, y), big)
        x += big.width + PAD
    y += row_h + 14
sheet.save(OUT)
print("saved %s  %s" % (OUT, sheet.size))
