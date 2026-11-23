// Reef UIArt · Affinity 绘制「UI 骨架三件套」：按钮基底 + 标题纸板 + 双线框
// 输出：桌面 reef-uiart\*.png（再由 Tools\uiart-chromepass.py 过纸/墨后处理写回项目）
// 规则：中性白 + alpha（颜色永远由 UITheme token 给），尺寸与 9-slice 边不得变：
//   btn_plate_9s 64×64 border 12 四边 / titleplate_9s 64×56 border 20,0,20,0
//   rule_ring_9s 32×32 border 8 四边 / rule_ring_worn_9s 32×32 border 8 四边
const { Document, NewDocumentOptions, FileExportOptions, FileExportArea } = require('/document.js');
const { AddChildNodesCommandBuilder } = require('/commands.js');
const { Rectangle } = require('/geometry.js');
const { ShapeNodeDefinition } = require('/nodes.js');
const { ShapeRectangle } = require('/shapes.js');
const { Colour } = require('/colours.js');
const { FillDescriptor, BlendMode } = require('/fills.js');
const { Application } = require('/application.js');
const { UnitType } = require('/units.js');
const nativeColours = require('affinity:colours');

function grey(v, a) {                       // 中性灰 + alpha（染色时只乘颜色，明度差即压印差）
    const s = new nativeColours.RGBA8();
    s.r = v; s.g = v; s.b = v; s.alpha = Math.round(a * 255);
    return Colour.createRGBA8(s);
}
function fill(c) { return FillDescriptor.createSolid(c, BlendMode.Normal); }

const DPI = 96;
const ROOT = Application.userDesktopPath + '\\reef-uiart';
const SRC = ROOT + '\\src';

function newDoc(w, h) {
    const o = NewDocumentOptions.createDefault();
    o.units = UnitType.Pixel; o.width = w; o.height = h; o.dpi = DPI; o.viewDpi = DPI;
    o.isTransparentBackground = true;
    const d = Document.create(o);
    d.units = UnitType.Pixel;
    return d;
}
function plate(x, y, w, h, v, a, r) {       // 实心矩形（可圆角）
    const s = ShapeRectangle.create();
    if (r) { try { for (const c of [s.topLeft, s.topRight, s.bottomLeft, s.bottomRight]) c.radius = r; } catch (e) { } }
    return ShapeNodeDefinition.create(s, new Rectangle(x, y, w, h), fill(grey(v, a)), null, null, null);
}
function toArray(v) {
    if (!v) return [];
    if (v.toArray) return v.toArray();
    if (typeof v.length === 'number') { const a = []; for (let i = 0; i < v.length; ++i) a.push(v[i]); return a; }
    return [];
}

// ── 1) 按钮基底：纸板 + 上受光 + 下压痕 + 底部坐边 ──────────────────────────
function buildButton(b) {
    b.addNode(plate(0, 0, 64, 64, 244, 0.96, 3));        // 底板（略灰，留出提亮余量）
    b.addNode(plate(2, 0.5, 60, 4, 255, 0.55, 1));       // 上受光带
    b.addNode(plate(3, 1.5, 58, 2, 255, 0.70, 0));       // 上受光高光
    b.addNode(plate(2, 57, 60, 6, 208, 0.55, 1));        // 下压痕（拉长到边）
    b.addNode(plate(3, 60, 58, 3, 176, 0.42, 0));        // 下压痕最深一条
    b.addNode(plate(1, 62, 62, 1.5, 150, 0.30, 0));      // 底边坐线
    b.addNode(plate(61.5, 2, 1.5, 60, 168, 0.22, 0));    // 右侧略暗（纸厚）
}

// ── 2) 标题纸板：纸板 + 下沿 2px 墨线（只看左右两刀，纵向结构抗拉伸）────────
function buildTitle(b) {
    b.addNode(plate(0, 0, 64, 56, 250, 1.0, 0));         // 纸板
    b.addNode(plate(0, 47, 64, 4, 232, 0.85, 0));        // 栏下压深带
    b.addNode(plate(0, 51, 64, 3, 214, 0.80, 0));        // 过渡
    b.addNode(plate(0, 54, 64, 2, 46, 0.90, 0));         // 下沿 2px 墨线（乘上纸色后即为深墨线）
}

// ── 3) 细墨线环：1px 印刷线 + 内圈极淡洇墨 ────────────────────────────────
function buildRing(b) {
    b.addNode(plate(0, 0, 32, 1, 255, 0.95, 0));         // 上
    b.addNode(plate(0, 31, 32, 1, 255, 0.92, 0));        // 下
    b.addNode(plate(0, 0, 1, 32, 255, 0.94, 0));         // 左
    b.addNode(plate(31, 0, 1, 32, 255, 0.93, 0));        // 右
    b.addNode(plate(1, 1, 30, 1, 255, 0.20, 0));         // 内圈洇墨（下缘）
    b.addNode(plate(1, 1, 1, 30, 255, 0.18, 0));         // 内圈洇墨（右缘）
}

// ── 4) 做旧墨线环：分段 + 断口 + 墨色不匀（手绘感，但几何是矢量对齐的）──────
function buildRingWorn(b) {
    b.addNode(plate(0, 0, 32, 1.4, 255, 0.88, 0));       // 上（略粗）
    b.addNode(plate(0, 0, 12, 1.4, 255, 0.96, 0));       //   上左端加压
    b.addNode(plate(0, 30.6, 13, 1.4, 255, 0.74, 0));    // 下·左段
    b.addNode(plate(18, 30.6, 14, 1.4, 255, 0.82, 0));   // 下·右段（中间 5px 断口）
    b.addNode(plate(0, 0, 1.4, 31, 255, 0.90, 0));       // 左
    b.addNode(plate(30.6, 0, 1.4, 9, 255, 0.86, 0));     // 右·上段
    b.addNode(plate(30.6, 12, 1.4, 19, 255, 0.78, 0));   // 右·下段（中间 3px 断口）
    b.addNode(plate(2, 2, 28, 1, 255, 0.16, 0));         // 内圈洇墨
    b.addNode(plate(2, 2, 1, 28, 255, 0.14, 0));
}

const ASSETS = [
    { name: 'btn_plate_9s', w: 64, h: 64, build: buildButton },
    { name: 'titleplate_9s', w: 64, h: 56, build: buildTitle },
    { name: 'rule_ring_9s', w: 32, h: 32, build: buildRing },
    { name: 'rule_ring_worn_9s', w: 32, h: 32, build: buildRingWorn }
];

const report = [];
for (const spec of ASSETS) {
    const row = { name: spec.name, ok: false };
    try {
        const doc = newDoc(spec.w, spec.h);
        const b = AddChildNodesCommandBuilder.create();
        spec.build(b);
        const cmd = b.createCommand(true);
        doc.executeCommand(cmd);
        row.nodes = toArray(cmd.newNodes).length;
        try { doc.saveAs(SRC + '\\' + spec.name + '.afdesign'); row.saved = true; } catch (e) { row.saved = 'ERR ' + e; }
        doc.export(ROOT + '\\' + spec.name + '.png', FileExportOptions.createWithPresetName('PNG'),
            FileExportArea.createForWholeDocument(), null);
        row.ok = true;
        try { doc.close(); } catch (e) { row.closed = 'ERR ' + e; }
    } catch (e) { row.error = String(e); }
    report.push(row);
    console.log(JSON.stringify(row));
}
console.log('DONE ' + report.filter(r => r.ok).length + '/' + ASSETS.length);
