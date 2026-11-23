# -*- coding: utf-8 -*-
"""
gen-uiart.py —— 「暗礁」UI 素材离线生成器（Lead 唯一属主）

用途：在不依赖 Unity / Affinity 的前提下，用 Pillow + numpy 直接产出 UGUI 可用的
9-slice 贴图与印章贴图，落到 Assets/Resources/UIArt/，并写出配套 .meta
（spriteMode=Single / textureType=Sprite / spriteBorder 已填 / 无 mipmap / 不压缩）。

设计规则（硬约束，改素材前先读）：
1. 调色板逐字取自 Assets/UI/UITheme.cs —— 素材不许自创色。
2. 9-slice 安全：PNG 的「拉伸中区」(border 之内) 只允许细颗粒噪声；
   一切结构（做旧边、胶印线、特征）只放边框环里。
   若必须让中区有变化，变化方向必须垂直于拉伸方向（横向拉伸 → 只沿 y 变化）。
3. 确定性：每个素材固定 seed，重跑得到逐字节相同的结果。
4. 只增不改：不改 UITheme、不改既有 Assets 代码；生成物只在 Assets/Resources/UIArt/。

用法：
    python Tools/gen-uiart.py                 # 生成全部
    python Tools/gen-uiart.py --only paper    # 只生成名称含 paper 的
    python Tools/gen-uiart.py --sheet         # 生成后额外输出对照图 Tools/.uiart/contact-sheet.png
"""

import argparse
import hashlib
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

# ── 路径 ──────────────────────────────────────────────────────────────────────
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "UIArt")
SHEET_DIR = os.path.join(ROOT, "Tools", ".uiart")
FONT_PATH = os.path.join(ROOT, "Assets", "Fonts", "NotoSerifSC-VariableFont_wght.ttf")

# ── 调色板（逐字取自 Assets/UI/UITheme.cs:6-20,35,115-146,154-171）─────────────
PAPER_BG = (231, 222, 200)      # #E7DEC8
PAPER_TOP = (240, 233, 214)     # #F0E9D6
INK_PRIMARY = (42, 35, 24)      # #2A2318
INK_SECONDARY = (90, 81, 69)    # #5A5145
INK_MUTED = (138, 128, 114)     # #8A8072
SEAL_RED = (160, 48, 24)        # #A03018
ACCENT_GOLD = (176, 138, 62)    # #B08A3E
DIVIDER = (201, 191, 166)       # #C9BFA6
REDACTED = (58, 52, 44)         # #3A342C
BTN_HOVER_BG = (230, 218, 190)  # #E6DABE
BTN_PRESSED_BG = (211, 200, 174)  # #D3C8AF
BTN_DISABLED_BG = (214, 205, 181)  # #D6CDB5
SHADOW = (42, 35, 24)           # ShadowColor 的 RGB
DOSSIER_BG = (201, 191, 166)    # DossierTagBg 的 RGB（alpha 另计）


def rgba(c, a=1.0):
    """(r,g,b) + 0..1 alpha -> (r,g,b,a255)"""
    return (int(c[0]), int(c[1]), int(c[2]), int(round(max(0.0, min(1.0, a)) * 255)))


# ── 基础工具 ──────────────────────────────────────────────────────────────────
def blur(arr, sigma):
    """对 float 数组做高斯模糊（借道 uint8，量化误差 <1/255，可接受）。"""
    if sigma <= 0:
        return arr
    lo, hi = float(np.min(arr)), float(np.max(arr))
    if hi - lo < 1e-6:
        return arr.copy()
    u8 = ((arr - lo) / (hi - lo) * 255.0).astype(np.uint8)
    b = np.asarray(Image.fromarray(u8).filter(ImageFilter.GaussianBlur(sigma)), dtype=np.float32) / 255.0
    return b * (hi - lo) + lo


def edge_dist(size):
    """每像素到最近边缘的距离（0 = 贴边）。"""
    idx = np.arange(size, dtype=np.float32)
    d = np.minimum(idx, size - 1 - idx)
    return np.minimum(d[:, None], d[None, :])


def ring_mask(size, border):
    """1 = 在边框环内（不被拉伸），0 = 在中区（会被拉伸）。"""
    d = edge_dist(size)
    return np.clip((border - d) / max(1.0, border * 0.35), 0.0, 1.0)


def fibers(rng, size, count, strength):
    """随机横向纤维（纸的帘纹），只保留极低幅度。"""
    f = np.zeros((size, size), np.float32)
    for _ in range(count):
        y = int(rng.integers(0, size))
        x0 = int(rng.integers(0, size))
        ln = int(rng.integers(size // 3, size))
        xs = [(x0 + i) % size for i in range(ln)]
        f[y, xs] += rng.uniform(-strength, -strength * 0.35)
    return blur(f, 0.7)


def paper_plate(base, seed, edge_darken=13.0, speck_count=7, size=64, border=18,
                grain=1.7, mottle=3.4, top_light=3.0, neutral=False):
    """纸底贴图。

    neutral=True 时输出「白 + 亮度扰动」：程序侧仍用 UITheme 的纸色去 tint，
    贴图只负责颗粒/做旧（色调 100% 由 token 决定，状态换色不会打架）。
    """
    rng = np.random.default_rng(seed)
    g = blur(rng.normal(0.0, 1.0, (size, size)), 0.55) * grain
    m = blur(rng.normal(0.0, 1.0, (size, size)), 5.0) * mottle
    ring = ring_mask(size, border)
    m = m * (0.18 + 0.82 * ring)          # 中区几乎抹平花纹（9-slice 拉伸安全）
    f = fibers(rng, size, 16, 1.5)
    lum = g + m + f

    # 边部做旧：越靠边越暗（smoothstep 收尾，避免 9-slice 边界出现硬折痕）
    d = edge_dist(size)
    t = np.clip((border - d) / float(border), 0.0, 1.0)
    age = (t * t * (3.0 - 2.0 * t)) ** 1.25
    lum -= age * edge_darken

    # 顶部内高光（纸被压出的一点点光）
    hi = np.zeros((size, size), np.float32)
    hi[1:4, :] += top_light
    lum += blur(hi, 0.8)

    if neutral:
        # 以「中区」为基准归一到 0：中区 → 253 白，做旧边 → 明显更深
        mid = ring < 0.5
        ref = float(np.mean(lum[mid])) if np.any(mid) else 0.0
        rgb = np.clip(253.0 + (lum - ref)[..., None], 0, 255) * np.ones((1, 1, 3), np.float32)
    else:
        rgb = np.clip(np.array(base, np.float32)[None, None, :] + lum[..., None], 0, 255)

    # 霉斑：只放在边框环里
    for _ in range(speck_count):
        ang = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(border * 0.25, border * 0.95)
        cx = size / 2 + math.cos(ang) * (size / 2 - r)
        cy = size / 2 + math.sin(ang) * (size / 2 - r)
        rad = rng.uniform(0.6, 1.8)
        yy, xx = np.mgrid[0:size, 0:size]
        spot = np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * rad * rad)))
        mask = spot * ring
        if neutral:
            rgb[..., 0] -= mask * rng.uniform(6, 14)
            rgb[..., 1] -= mask * rng.uniform(8, 18)
            rgb[..., 2] -= mask * rng.uniform(10, 22)
        else:
            rgb[..., 0] -= mask * rng.uniform(6, 14)
            rgb[..., 1] -= mask * rng.uniform(8, 18)
            rgb[..., 2] -= mask * rng.uniform(10, 22)

    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    return img


def frame_layer(size, border, inset, thickness, color, alpha, rng, jitter=0.18, corner_boost=0.35):
    """一圈印刷框线：中段均匀（抗拉伸），抖动只沿「垂直于拉伸方向」的轴。

    - 水平线段（上/下边）沿 y 抖动：横向拉伸后 y 方向的不均匀性完整保留；
    - 垂直线段（左/右边）沿 x 抖动：纵向拉伸后同样保留。
    """
    d = edge_dist(size)
    line = np.clip(thickness - np.abs(d - inset), 0, 1)
    ring = ring_mask(size, border)
    yy = np.arange(size, dtype=np.float32)[:, None]
    xx = np.arange(size, dtype=np.float32)[None, :]
    jy = (np.sin(yy * 0.9 + rng.uniform(0, 6.28)) * 0.5 + 0.5) * jitter
    jx = (np.sin(xx * 0.9 + rng.uniform(0, 6.28)) * 0.5 + 0.5) * jitter
    # 上/下边主要受 jy 影响，左/右边主要受 jx 影响
    horiz = np.clip(1.0 - np.abs(yy - (size - 1) / 2.0) / (size / 2.0), 0, 1)   # ≈1 在上下边
    vert = np.clip(1.0 - np.abs(xx - (size - 1) / 2.0) / (size / 2.0), 0, 1)    # ≈1 在左右边
    a = line * ((1.0 - jitter) + jy * (1.0 - vert) + jx * vert)
    # 转角处加重（印版交叠）
    corner = np.clip(1.0 - np.minimum.reduce([xx + yy, xx + (size - 1 - yy),
                                              (size - 1 - xx) + yy,
                                              (size - 1 - xx) + (size - 1 - yy)]) / (border * 0.9), 0, 1)
    a = a * (1.0 + corner_boost * corner * ring)
    return np.clip(a, 0, 1) * alpha


def save(img, name, border, filter_mode=1, compression=0, sRGB=True, alpha_transparency=True):
    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, name + ".png")
    img.save(png, "PNG", optimize=True)
    meta = os.path.join(OUT_DIR, name + ".png.meta")
    # 平铺贴图必须 Repeat(0)，否则消费端 uvRect 平铺会全采样到边缘像素（2026-02 复核发现的坑）
    wrap = 0 if name in WRAP_REPEAT else 1
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(texture_meta(name, border, filter_mode, compression, sRGB, alpha_transparency, wrap))
    w, h = img.size
    print("  %-26s %3dx%-4d border=%-18s %6d B" % (name, w, h, str(border), os.path.getsize(png)))


def texture_meta(name, border, filter_mode, compression, sRGB, alpha_transparency, wrap=1):
    """Unity 6 TextureImporter .meta（spriteMode=Single / textureType=Sprite / 无 mipmap / 不压缩）。

    spriteMeshType 固定 0（Full Rect）：9-slice 必须用全矩形网格，Tight 会把有透明边的
    切片按轮廓重算网格，border 拉伸会失真（Unity 手册 9-slice 要求 Full Rect）。
    wrap 由调用方按素材用途给：平铺贴图 Repeat(0)，其余 Clamp(1)。
    """
    guid = hashlib.md5(("reef-uiart-" + name).encode("utf-8")).hexdigest()
    l, b, r, t = border
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: {1 if sRGB else 0}
    linearTexture: {0 if sRGB else 1}
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: {filter_mode}
    aniso: 1
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {l}, y: {b}, z: {r}, w: {t}}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: {1 if alpha_transparency else 0}
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: {compression}
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


# ── 素材：纸底（中性白，程序用 token 纸色 tint）────────────────────────────────
def build_paper_plate():
    return paper_plate(PAPER_TOP, seed=1101, edge_darken=12.0, speck_count=7, neutral=True)


def build_titleplate():
    """标题栏纸板：64×56 / border(20,0,20,0)。只有沿 y 的结构（横向拉伸后完整保留）。"""
    w, h, border = 64, 56, 20
    rng = np.random.default_rng(2301)
    plate = paper_plate(PAPER_TOP, seed=2302, edge_darken=6.0, speck_count=2, size=64, border=20,
                        grain=1.1, mottle=1.6, top_light=0.0, neutral=True).crop((0, 0, w, h))
    arr = np.asarray(plate.convert("RGB"), np.float32)
    # 顶部 2px 受光（压印上缘），底部 3px 略沉（与下沿线衔接）
    arr[0, :, :] += 7.0
    arr[1, :, :] += 3.0
    for y in (h - 1, h - 2, h - 3):
        arr[y, :, :] -= (14.0, 14.0, 14.0)
    for y in (h - 4,):
        arr[y, :, :] -= (5.0, 5.0, 5.0)
    # 沿 y 的轻微污渍（横向拉伸后仍在）
    arr += blur(rng.normal(0, 1, (h, 1)), 1.4)[:, 0][:, None, None] * 2.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


# ── 素材：印刷框线（中性白，程序用 ChromeOuterColor/ChromeInnerColor tint）────
def build_rule_ring(seed, wear, jitter, corner_boost=0.35):
    """32×32 / border 8：贴图最外缘 1px 的白环，内部透明。

    抖动只出现在「垂直于拉伸方向」的轴上：上/下边沿 y 抖、左/右边沿 x 抖，
    因此 9-slice 横向/纵向拉伸后墨迹不匀仍然保留，中段不会出现拉伸色带。
    """
    size, border = 32, 8
    rng = np.random.default_rng(seed)
    a = frame_layer(size, border, inset=0, thickness=1.0, color=(255, 255, 255), alpha=1.0,
                    rng=rng, jitter=jitter, corner_boost=corner_boost)
    # 缺墨：低频噪声阈值化（只作用于环本身）+ 几处断墨缺口
    w = blur(rng.normal(0, 1, (size, size)), 1.6)
    w = w / max(1e-6, np.max(np.abs(w)))
    a = a * np.clip(1.0 - wear * np.clip(w, 0, 1) * 1.8, 0.15, 1.0)
    for _ in range(3):
        ln = int(rng.integers(2, 5))
        horiz = bool(rng.integers(0, 2))
        p = int(rng.integers(1, size - 2))
        if horiz:
            a[p, 0:ln] *= 0.25
            a[p, size - ln:size] *= 0.25
        else:
            a[0:ln, p] *= 0.25
            a[size - ln:size, p] *= 0.25
    rgb = np.zeros((size, size, 4), np.float32)
    rgb[..., 0:3] = 255.0
    rgb[..., 3] = np.clip(a, 0, 1) * 255.0
    return Image.fromarray(rgb.astype(np.uint8), "RGBA")


# ── 素材：按钮压印板（中性白，程序用五态色 tint）──────────────────────────────
def build_button_plate():
    size, border = 64, 12
    rng = np.random.default_rng(2601)
    plate = paper_plate(PAPER_TOP, seed=2602, edge_darken=3.0, speck_count=1, size=size, border=border,
                        grain=1.2, mottle=1.4, top_light=0.0, neutral=True)
    arr = np.asarray(plate.convert("RGB"), np.float32)
    arr[0, :, :] += 9.0            # 上缘受光
    arr[1, :, :] += 3.0
    arr[-1, :, :] -= 12.0          # 下缘压痕
    arr[-2, :, :] -= 5.0
    # 外框 1px（比纸面略深的压印边）
    ring = frame_layer(size, border, inset=0, thickness=1.0, color=(255, 255, 255), alpha=1.0,
                       rng=rng, jitter=0.14)
    arr -= ring[..., None] * 12.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


# ── 素材：打码条 / 档号角标 / 装订点（中性白）──────────────────────────────────
def build_redaction_bar():
    w, h, border = 96, 16, 12
    rng = np.random.default_rng(2401)
    ink = np.ones((h, w), np.float32)
    # 干墨：端部随机缺墨（端部不参与拉伸，因此 9-slice 拉伸后仍然只在两端）
    dry = blur(rng.normal(0, 1, (1, w)), 1.0)[0]
    end = np.zeros(w, np.float32)
    end[:border] = 1.0
    end[-border:] = 1.0
    ink *= 1.0 - np.clip(dry * 0.55, 0, 0.5) * end
    # 上下缘毛边
    ink[0, :] *= 0.55
    ink[1, :] *= 0.90
    ink[-2, :] *= 0.92
    ink[-1, :] *= 0.65
    arr = np.zeros((h, w, 4), np.float32)
    arr[..., 0:3] = 255.0
    arr[..., 3] = np.clip(ink, 0, 1) * 255.0
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def build_dossier_plate():
    w, h, border = 48, 22, 12
    plate = paper_plate(DOSSIER_BG, seed=2403, edge_darken=3.0, speck_count=1, size=48, border=6,
                        grain=1.1, mottle=1.4, top_light=0.0, neutral=True).resize((w, h), Image.LANCZOS)
    arr = np.asarray(plate.convert("RGB"), np.float32)
    arr[h - 1, :, :] -= 26.0      # 底边 1px 压深（程序用 DossierTagBg 色 tint）
    arr[h - 2, :, :] -= 8.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def build_binding_dot():
    size = 8
    rng = np.random.default_rng(2404)
    m = np.zeros((size, size), np.float32)
    m[2:6, 2:6] = 1.0
    m = blur(m, 0.7)
    m = np.clip((m - 0.28) * 2.4, 0, 1)
    m *= 1.0 - blur(rng.normal(0, 1, (size, size)), 0.5) * 0.12
    arr = np.zeros((size, size, 4), np.float32)
    arr[..., 0:3] = 255.0
    arr[..., 3] = np.clip(m, 0, 1) * 255.0
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


# ── 素材：叠层颗粒 / 网点 / 暗角 ──────────────────────────────────────────────
def build_grain_tile():
    size = 256
    rng = np.random.default_rng(2501)
    g = blur(rng.normal(0, 1, (size, size)), 0.6)
    g = g / max(1e-6, np.max(np.abs(g)))
    arr = np.zeros((size, size, 4), np.float32)
    dark = np.clip(-g, 0, 1)
    light = np.clip(g, 0, 1)
    for ch in range(3):
        arr[..., ch] = np.where(dark > light, SHADOW[ch], 255)
    arr[..., 3] = (dark * 0.28 + light * 0.20) * 255.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def build_halftone_tile():
    size, step = 64, 8
    arr = np.zeros((size, size, 4), np.float32)
    m = np.zeros((size, size), np.float32)
    for y in range(0, size, step):
        for x in range(0, size, step):
            m[y + 1:y + 3, x + 1:x + 3] = 1.0
    m = blur(m, 0.7)
    m = np.clip((m - 0.22) * 1.5, 0, 1)
    for ch in range(3):
        arr[..., ch] = INK_PRIMARY[ch]
    arr[..., 3] = m * 0.22 * 255.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def build_vignette():
    size = 256
    rng = np.random.default_rng(2503)
    yy, xx = np.mgrid[0:size, 0:size]
    cx = cy = (size - 1) / 2.0
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / (size / 2.0)
    v = np.clip((r - 0.55) / 0.55, 0, 1) ** 1.6
    v = blur(v, 3.0) * (1.0 + blur(rng.normal(0, 1, (size, size)), 2.0) * 0.06)
    arr = np.zeros((size, size, 4), np.float32)
    for ch in range(3):
        arr[..., ch] = SHADOW[ch]
    arr[..., 3] = np.clip(v, 0, 1) * 0.62 * 255.0
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


# ── 素材：朱砂印章（文字用项目自带 Noto Serif SC 烘焙）────────────────────────
def load_font(px, weight=700):
    try:
        f = ImageFont.truetype(FONT_PATH, px)
        try:
            f.set_variation_by_axes([weight])
        except Exception:
            pass
        return f
    except Exception as exc:  # pragma: no cover
        print("  !! font load failed: %s" % exc)
        return ImageFont.load_default()


def stamp_image(text, w, h, seed, scale=4, font_ratio=0.52, ring=True, letter_space=6):
    """橡皮图章：双框 + 文字，做旧（缺墨、毛边、重影）。"""
    rng = np.random.default_rng(seed)
    W, H = w * scale, h * scale
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    pad = int(3 * scale)
    if ring:
        d.rectangle([pad, pad, W - 1 - pad, H - 1 - pad], outline=255, width=scale)
        d.rectangle([pad + int(3.2 * scale), pad + int(3.2 * scale),
                     W - 1 - pad - int(3.2 * scale), H - 1 - pad - int(3.2 * scale)],
                    outline=255, width=max(1, int(0.8 * scale)))
    font = load_font(int(min(H * font_ratio, W / max(1, len(text)) * 0.92)))
    # 逐字绘制以获得字距控制
    total = 0
    sizes = []
    for ch in text:
        bbox = d.textbbox((0, 0), ch, font=font)
        sizes.append(bbox[2] - bbox[0])
        total += bbox[2] - bbox[0]
    total += letter_space * scale * max(0, len(text) - 1)
    x = (W - total) / 2.0
    for ch, cw in zip(text, sizes):
        bbox = d.textbbox((0, 0), ch, font=font)
        d.text((x - bbox[0], (H - (bbox[3] - bbox[1])) / 2.0 - bbox[1]), ch, font=font, fill=255)
        x += cw + letter_space * scale

    a = np.asarray(m, np.float32) / 255.0

    # 缺墨：低频噪声阈值化
    wear = blur(rng.normal(0, 1, (H, W)), 2.2 * scale)
    wear = wear / max(1e-6, np.max(np.abs(wear)))
    a = a * np.clip(0.62 + wear * 0.75, 0, 1.15)
    # 干擦条纹（垂直于文字走向）
    for _ in range(int(2.5 * scale)):
        y0 = int(rng.integers(0, H))
        th = int(rng.integers(1, max(2, scale // 2 + 1)))
        a[y0:y0 + th, :] *= rng.uniform(0.35, 0.75)
    # 毛边
    a = blur(a, 0.55 * scale)
    a = np.clip((a - 0.22) * 1.55, 0, 1)
    # 重影（图章二次落印）
    ghost = np.roll(np.roll(a, int(0.6 * scale), axis=0), int(0.4 * scale), axis=1) * 0.22
    a = np.clip(a + ghost, 0, 1)

    arr = np.zeros((H, W, 4), np.float32)
    # 墨色随浓淡轻微变化（浓处更暗）
    for ch in range(3):
        arr[..., ch] = SEAL_RED[ch] * (0.86 + 0.14 * np.clip(a, 0, 1))
    arr[..., 3] = a * 0.88 * 255.0
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    return img.resize((w, h), Image.LANCZOS)


# ── 组装：素材清单 ────────────────────────────────────────────────────────────
# 需要 Repeat 平铺的素材（其余一律 Clamp）：消费端用 uvRect 把这两张铺满全屏。
WRAP_REPEAT = {"grain_tile", "halftone_tile"}

# 三枚朱砂印章 + 按钮基底/标题纸板/两枚墨线环改由 Affinity 原生绘制（矢量文字与印刷框线），
# 源与流程见：
#   Tools/.uiart/art-batch.js         在 Affinity 里批量画印章等 9 件并导出 PNG（MCP execute_script）
#   Tools/.uiart/art-chrome.js        同上，画按钮基底与边框样式 4 件
#   Tools/uiart-inkpass.py            印章：过一遍朱砂印泥后处理，写回 Assets/Resources/UIArt/
#   Tools/uiart-chromepass.py         基底/边框：过一遍纸墨后处理（颗粒取极轻，避免九宫拉伸抹成条带）
# 这里**只登记清单与 border 规格、不再用 PIL 重画**，以免 gen-uiart.py 把 Affinity 版覆盖回旧版。
# 需要旧版时：python Tools/gen-uiart.py --force-pil
AFFINITY_ASSETS = {
    "stamp_weikaifang", "stamp_shentou", "stamp_jimi",
    "btn_plate_9s", "titleplate_9s", "rule_ring_9s", "rule_ring_worn_9s",
}


def build_all(only=None):
    items = []

    def add(name, border, img, filter_mode=1, compression=0):
        if only and only.lower() not in name.lower():
            return
        if name in AFFINITY_ASSETS:
            img = None                       # Affinity 版：清单里保留，但不由 PIL 生成像素
        items.append((name, border, img, filter_mode, compression))

    add("paper_plate_9s", (18, 18, 18, 18), build_paper_plate())
    add("titleplate_9s", (20, 0, 20, 0), build_titleplate())
    add("rule_ring_9s", (8, 8, 8, 8), build_rule_ring(2201, wear=0.30, jitter=0.20), filter_mode=0)
    add("rule_ring_worn_9s", (8, 8, 8, 8), build_rule_ring(2202, wear=0.55, jitter=0.34), filter_mode=0)
    add("btn_plate_9s", (12, 12, 12, 12), build_button_plate())
    add("redaction_bar_9s", (12, 0, 12, 0), build_redaction_bar())
    add("dossier_plate_9s", (12, 0, 12, 0), build_dossier_plate())
    add("binding_dot", (0, 0, 0, 0), build_binding_dot(), filter_mode=0)
    add("grain_tile", (0, 0, 0, 0), build_grain_tile())
    add("halftone_tile", (0, 0, 0, 0), build_halftone_tile(), filter_mode=0)
    add("vignette", (0, 0, 0, 0), build_vignette())
    add("stamp_weikaifang", (0, 0, 0, 0), None)
    add("stamp_shentou", (0, 0, 0, 0), None)
    add("stamp_jimi", (0, 0, 0, 0), None)
    return items

def contact_sheet(items):
    """把已生成的素材排成对照图，供人工/Lead 目视检查。"""
    os.makedirs(SHEET_DIR, exist_ok=True)
    scale = 2
    cell_w, cell_h = 200, 120
    cols = 5
    rows = int(math.ceil(len(items) / cols))
    W, H = cols * cell_w, rows * cell_h
    sheet = Image.new("RGBA", (W, H), rgba(PAPER_BG, 1.0))
    font = load_font(14, 500)
    d = ImageDraw.Draw(sheet)
    for i, (name, _border, _img, _f, _c) in enumerate(items):
        cx = (i % cols) * cell_w
        cy = (i // cols) * cell_h
        # 棋盘底，方便看透明区
        for yy in range(cy, cy + cell_h, 10):
            for xx in range(cx, cx + cell_w, 10):
                if ((xx - cx) // 10 + (yy - cy) // 10) % 2 == 0:
                    d.rectangle([xx, yy, xx + 9, yy + 9], fill=(255, 255, 255, 255))
        p = os.path.join(OUT_DIR, name + ".png")
        im = Image.open(p).convert("RGBA")
        big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        px = cx + (cell_w - big.width) // 2
        py = cy + 26 + max(0, (cell_h - 46 - big.height) // 2)
        sheet.alpha_composite(big, (max(cx, px), max(cy + 4, py)))
        d.text((cx + 8, cy + 6), name, font=font, fill=rgba(INK_PRIMARY, 1.0))
    out = os.path.join(SHEET_DIR, "contact-sheet.png")
    sheet.convert("RGB").save(out, "PNG")
    print("  contact sheet -> %s" % out)
    return out


def pixel_selfcheck(items):
    """像素级自检：两张平铺贴层 + 暗角。

    为什么需要：这两张贴图在运行期是 **Repeat 平铺**（`UIArt.BuildScreenOverlays` 里 `uvRect` 宽
    1920/64 = 30 倍），一旦图案不是整周期，屏幕上就会出现接缝；而 meta 自检查不出这个。
    """
    bad = []
    for name, _border, img, _fm, _comp in items:
        if name not in ("halftone_tile", "grain_tile", "vignette"):
            continue
        a = np.asarray(img.convert("RGBA"), dtype=np.uint8)[..., 3].astype(np.float32) / 255.0
        if name == "halftone_tile":
            if a.max() <= 0:
                bad.append("halftone_tile: 全透明")
            elif not (np.array_equal(a, np.roll(a, 8, axis=0)) and np.array_equal(a, np.roll(a, 8, axis=1))):
                bad.append("halftone_tile: alpha 不是 8px 整周期（Repeat 平铺会出接缝）")
        elif name == "grain_tile":
            cover = float((a > 0.01).mean())
            mean = float(a.mean())
            if cover < 0.5:
                bad.append("grain_tile: 覆盖率 %.2f < 0.5（叠层会看成斑块而不是颗粒）" % cover)
            if not (0.005 <= mean <= 0.15):
                bad.append("grain_tile: 平均 alpha %.3f 不在 0.005-0.15（太淡看不见 / 太浓糊成一片）" % mean)
        else:
            if float(a.max()) < 0.5:
                bad.append("vignette: 峰值 alpha %.2f < 0.5（暗角会看不见）" % float(a.max()))
    return bad


def force_pil(name):
    """--force-pil 用：按旧版参数重画素材，仅在需要退回 PIL 版时才走这条路。"""
    if name == "stamp_weikaifang":
        return stamp_image("未开放", 176, 72, 2701)
    if name == "stamp_shentou":
        return stamp_image("已渗透", 176, 72, 2702)
    if name == "stamp_jimi":
        return stamp_image("机密", 128, 128, 2703, font_ratio=0.22, letter_space=8)
    if name == "btn_plate_9s":
        return build_button_plate()
    if name == "titleplate_9s":
        return build_titleplate()
    if name == "rule_ring_9s":
        return build_rule_ring(2201, wear=0.30, jitter=0.20)
    if name == "rule_ring_worn_9s":
        return build_rule_ring(2202, wear=0.55, jitter=0.34)
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default=None)
    ap.add_argument("--sheet", action="store_true")
    ap.add_argument("--force-pil", "--force-stamps", dest="force_pil", action="store_true",
                    help="用 PIL 重画 Affinity 版素材（默认保留 Affinity 版，不覆盖）")
    args = ap.parse_args()

    print("UIArt out: %s" % OUT_DIR)
    items = build_all(args.only)
    if not items:
        print("nothing matched --only=%s" % args.only)
        return 1
    for name, border, img, fm, comp in items:
        if img is None:                      # Affinity 产出的素材：文件已在磁盘上，别用 PIL 覆盖
            if args.force_pil:
                img = force_pil(name)
            else:
                print("keep %s (Affinity 版，跳过 PIL 重画)" % name)
                continue
        save(img, name, border, filter_mode=fm, compression=comp)
    # 自检：meta 里的三项硬字段必须与契约一致（P23/P25），不一致就直接报错，别把坏 meta 留在磁盘上
    bad = []
    for name, border, _img, _fm, _comp in items:
        text = open(os.path.join(OUT_DIR, name + ".png.meta"), encoding="utf-8").read()
        want_wrap = 0 if name in WRAP_REPEAT else 1
        l, b, r, t = border
        checks = [
            ("textureType: 8", "textureType: 8" in text),
            ("spriteMeshType: 0", "spriteMeshType: 0" in text),
            ("wrapU: %d" % want_wrap, "wrapU: %d" % want_wrap in text),
            ("wrapV: %d" % want_wrap, "wrapV: %d" % want_wrap in text),
            ("spriteBorder: {x: %d, y: %d, z: %d, w: %d}" % (l, b, r, t),
             "spriteBorder: {x: %d, y: %d, z: %d, w: %d}" % (l, b, r, t) in text),
        ]
        for label, ok in checks:
            if not ok:
                bad.append("%s: %s" % (name, label))
    if bad:
        print("META SELF-CHECK FAILED:")
        for x in bad:
            print("  - " + x)
        return 2
    print("meta self-check: %d asset(s) ok" % len(items))
    bad_px = pixel_selfcheck(items)
    if bad_px:
        print("PIXEL SELF-CHECK FAILED:")
        for x in bad_px:
            print("  - " + x)
        return 3
    print("pixel self-check: tiling layers ok")
    print("done: %d asset(s)" % len(items))
    if args.sheet:
        # 对照图使用磁盘上的全部素材，而不是本次过滤结果
        everything = build_all(None)
        metas = [(n, b, None, f, c) for (n, b, _i, f, c) in everything
                 if os.path.exists(os.path.join(OUT_DIR, n + ".png"))]
        contact_sheet(metas)
    return 0


if __name__ == "__main__":
    sys.exit(main())
