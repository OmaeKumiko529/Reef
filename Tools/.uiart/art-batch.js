// Reef UIArt · 用 Affinity 原生绘制整批 UI 素材（v1）
// 输出：桌面 reef-uiart\*.png（再由外部脚本复制回项目），同时把 .afdesign 源文件存进 reef-uiart\src\。
// 规则：可染色件一律「中性白 + alpha」（颜色仍由 Unity 侧 UITheme token 给），印章类保留朱砂本色。
const { Document, NewDocumentOptions, FileExportOptions, FileExportArea } = require('/document.js');
const { AddChildNodesCommandBuilder } = require('/commands.js');
const { Rectangle } = require('/geometry.js');
const { ShapeNodeDefinition, ArtTextNodeDefinition } = require('/nodes.js');
const { ShapeRectangle } = require('/shapes.js');
const { StoryBuilder } = require('/storybuilder.js');
const { Colour } = require('/colours.js');
const { FillDescriptor, BlendMode } = require('/fills.js');
const { Application } = require('/application.js');
const { UnitType } = require('/units.js');
const { LineStyleDescriptor } = require('/linestyle.js');
const nativeColours = require('affinity:colours');

// ---------- 基础 ----------
function rgba(r, g, b, a) {
    const s = new nativeColours.RGBA8();
    s.r = r; s.g = g; s.b = b; s.alpha = a === undefined ? 255 : a;
    return Colour.createRGBA8(s);
}
const CINNABAR = rgba(168, 42, 26, 255);
const CINNABAR_PALE = rgba(168, 42, 26, 150);
const WHITE = a => rgba(255, 255, 255, Math.round(a * 255));
function plate(c) { return FillDescriptor.createSolid(c, BlendMode.Normal); }
function stroke(w) { return LineStyleDescriptor.createDefault(w); }

const DPI = 96;
const ROOT = Application.userDesktopPath + '\\reef-uiart';
const SRC = ROOT + '\\src';

function newDoc(w, h) {
    const opts = NewDocumentOptions.createDefault();
    opts.units = UnitType.Pixel;
    opts.width = w; opts.height = h; opts.dpi = DPI; opts.viewDpi = DPI;
    opts.isTransparentBackground = true;
    const doc = Document.create(opts);
    doc.units = UnitType.Pixel;
    return doc;
}

// 矩形（可圆角）：brush=null 表示只描边
function rect(x, y, w, h, brush, lineFill, weight, radius) {
    const shape = ShapeRectangle.create();
    if (radius) {
        try {
            for (const c of [shape.topLeft, shape.topRight, shape.bottomLeft, shape.bottomRight]) c.radius = radius;
        } catch (e) { console.log('  corner ERR ' + e); }
    }
    return ShapeNodeDefinition.create(shape, new Rectangle(x, y, w, h),
        brush ? plate(brush) : null,
        lineFill ? plate(lineFill) : null,
        lineFill ? stroke(weight) : null,
        null);
}

function artistic(text, x, y, size, colour, dpiScale) {
    const sb = StoryBuilder.create();
    sb.setToArtisticTextDefaultStyle(DPI, null);
    const ga = sb.glyphAtts;
    ga.brushFill = plate(colour);
    ga.height = size;
    sb.setGlyphAtts(ga);
    sb.addText(text);
    return ArtTextNodeDefinition.createFromStoryBuilder({ x, y }, sb);
}

// ---------- 素材表 ----------
const ASSETS = [];

// 1) 三个朱砂印章
function sealSpec(name, w, h, text, glyph, charGap) {
    return {
        name, w, h,
        build(b) {
            b.addNode(rect(2, 2, w - 4, h - 4, null, CINNABAR, 3.2, 2));      // 外框
            b.addNode(rect(9, 9, w - 18, h - 18, null, CINNABAR, 1.1, 1));    // 内线
            if (charGap) {
                // 竖排两字（方形章）
                const n = text.length;
                for (let i = 0; i < n; ++i)
                    b.addNode(artistic(text[i], Math.round((w - glyph) / 2) + 1,
                        Math.round((h - n * glyph) / 2 + glyph * (i + 0.82)), glyph, CINNABAR));
            } else {
                const advance = text.length * glyph * 1.02;
                b.addNode(artistic(text, Math.round((w - advance) / 2) + 2,
                    Math.round(h / 2 + glyph * 0.36), glyph, CINNABAR));
            }
        }
    };
}
ASSETS.push(sealSpec('stamp_weikaifang', 176, 72, '未开放', 38, false));
ASSETS.push(sealSpec('stamp_shentou', 176, 72, '已渗透', 38, false));
ASSETS.push(sealSpec('stamp_jimi', 128, 128, '机密', 40, true));

// 2) 双线框两枚（中性白）
ASSETS.push({
    name: 'rule_ring_9s', w: 32, h: 32,
    build(b) { b.addNode(rect(0.6, 0.6, 30.8, 30.8, null, WHITE(0.92), 1.2, 0)); }
});
ASSETS.push({
    name: 'rule_ring_worn_9s', w: 32, h: 32,
    build(b) {
        b.addNode(rect(0.6, 0.6, 30.8, 30.8, null, WHITE(0.72), 1.6, 0));
        b.addNode(rect(2.2, 2.2, 27.6, 27.6, null, WHITE(0.30), 0.8, 0));   // 内侧淡影，模拟双线磨损
    }
});

// 3) 标题纸板（只看左右两刀）
ASSETS.push({
    name: 'titleplate_9s', w: 64, h: 56,
    build(b) {
        b.addNode(rect(0, 0, 64, 56, WHITE(0.97), null, 0, 0));             // 纸板
        b.addNode(rect(0, 49, 64, 4, WHITE(0.55), null, 0, 0));             // 压深带
        b.addNode(rect(0, 53, 64, 2, WHITE(0.20), null, 0, 0));             // 底边墨线
    }
});

// 4) 按钮压印板
ASSETS.push({
    name: 'btn_plate_9s', w: 64, h: 64,
    build(b) {
        b.addNode(rect(0, 0, 64, 64, WHITE(0.90), null, 0, 3));             // 底
        b.addNode(rect(0, 0, 64, 3, WHITE(1.0), null, 0, 2));               // 上受光
        b.addNode(rect(0, 59, 64, 5, WHITE(0.30), null, 0, 2));             // 下压痕
    }
});

// 5) 档号角标纸片
ASSETS.push({
    name: 'dossier_plate_9s', w: 48, h: 22,
    build(b) {
        b.addNode(rect(0, 0, 48, 22, WHITE(0.92), null, 0, 0));
        b.addNode(rect(0, 19, 48, 3, WHITE(0.45), null, 0, 0));             // 底部略深
    }
});

// 6) 打码条（两端干墨收笔）
ASSETS.push({
    name: 'redaction_bar_9s', w: 96, h: 16,
    build(b) {
        b.addNode(rect(0, 1.5, 96, 13, WHITE(0.95), null, 0, 0));           // 主条
        b.addNode(rect(0, 0, 5, 3, WHITE(0.75), null, 0, 0));               // 左端碎笔
        b.addNode(rect(0, 13, 7, 3, WHITE(0.65), null, 0, 0));
        b.addNode(rect(91, 0.5, 5, 4, WHITE(0.70), null, 0, 0));            // 右端碎笔
        b.addNode(rect(89, 12, 7, 4, WHITE(0.60), null, 0, 0));
    }
});

// ---------- 执行 ----------
function toArray(v) {
    if (!v) return [];
    if (v.toArray) return v.toArray();
    if (typeof v.length === 'number') { const a = []; for (let i = 0; i < v.length; ++i) a.push(v[i]); return a; }
    return [];
}

try { require('/fs.js').fs.createDirectories(SRC); } catch (e) { console.log('mkdir ERR ' + e); }

const report = [];
for (const spec of ASSETS) {
    const row = { name: spec.name, ok: false, nodes: 0 };
    try {
        const doc = newDoc(spec.w, spec.h);
        const b = AddChildNodesCommandBuilder.create();
        spec.build(b);
        const cmd = b.createCommand(true);
        doc.executeCommand(cmd);
        row.nodes = toArray(cmd.newNodes).length;

        try { doc.saveAs(SRC + '\\' + spec.name + '.afdesign'); row.saved = true; } catch (e) { row.saved = 'ERR ' + e; }

        const png = ROOT + '\\' + spec.name + '.png';
        doc.export(png, FileExportOptions.createWithPresetName('PNG'), FileExportArea.createForWholeDocument(), null);
        row.png = png;
        row.ok = true;
        try { doc.close(); } catch (e) { row.closed = 'ERR ' + e; }
    } catch (e) {
        row.error = String(e);
    }
    report.push(row);
    console.log(JSON.stringify(row));
}
console.log('DONE ' + report.filter(r => r.ok).length + '/' + ASSETS.length);
