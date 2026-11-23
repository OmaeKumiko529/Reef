// Reef UIArt · Affinity 绘制「未开放」朱砂横章 v5
// v4 修正：填充/描边走 ShapeNodeDefinition.create(...)；v5 修正：brushFill 用 null 表示无填充、文字颜色走 StoryBuilder.glyphAtts.brushFill。
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
const { LineStyle, LineCap, LineJoin, LineStyleDescriptor } = require('/linestyle.js');
const nativeColours = require('affinity:colours');

function rgba(r, g, b, a) {
    const s = new nativeColours.RGBA8();
    s.r = r; s.g = g; s.b = b; s.alpha = a === undefined ? 255 : a;
    return Colour.createRGBA8(s);
}
const CINNABAR = rgba(168, 42, 26, 255);
const W = 176, H = 72, DPI = 96;
const TEXT = '未开放';
const GLYPH = 38;          // 字高（文档单位=px）

function plate(colour) { return FillDescriptor.createSolid(colour, BlendMode.Normal); }
function line(weight) {
    return LineStyleDescriptor.createDefault(weight);
}
function ring(inset, weight) {
    return ShapeNodeDefinition.create(ShapeRectangle.create(),
        new Rectangle(inset, inset, W - inset * 2, H - inset * 2),
        null, plate(CINNABAR), line(weight), null);
}
function artisticSealText() {
    const sb = StoryBuilder.create();
    sb.setToArtisticTextDefaultStyle(DPI, null);
    const ga = sb.glyphAtts;
    ga.brushFill = plate(CINNABAR);
    ga.height = GLYPH;
    sb.setGlyphAtts(ga);
    sb.addText(TEXT);
    const advance = TEXT.length * GLYPH;
    return ArtTextNodeDefinition.createFromStoryBuilder({ x: Math.round((W - advance) / 2) + 4, y: Math.round(H / 2 + GLYPH * 0.36) }, sb);
}

function toArray(v) {
    if (!v) return [];
    if (v.toArray) return v.toArray();
    if (typeof v.length === 'number') { const a = []; for (let i = 0; i < v.length; ++i) a.push(v[i]); return a; }
    return [];
}

try {
    const opts = NewDocumentOptions.createDefault();
    opts.units = UnitType.Pixel;
    opts.width = W; opts.height = H; opts.dpi = DPI; opts.viewDpi = DPI;
    opts.isTransparentBackground = true;
    const doc = Document.create(opts);
    doc.units = UnitType.Pixel;

    const b = AddChildNodesCommandBuilder.create();
    b.addNode(ring(2.0, 3.2));
    b.addNode(ring(9.0, 1.1));
    b.addNode(artisticSealText());
    const cmd = b.createCommand(true);
    doc.executeCommand(cmd);
    toArray(cmd.newNodes).forEach((n, i) => {
        let box = '?';
        try { box = JSON.stringify(n.getSpreadBaseBox()); } catch (e) { box = 'ERR'; }
        console.log('  [' + i + '] ' + (n[Symbol.toStringTag] || n) + ' box=' + box);
    });

    const dir = Application.userDesktopPath + '\\reef-uiart';
    try { require('/fs.js').fs.createDirectories(dir); } catch (e) { }
    const path = dir + '\\stamp-weikaifang-raw.png';
    doc.export(path, FileExportOptions.createWithPresetName('PNG'), FileExportArea.createForWholeDocument(), null);
    console.log('export -> ' + path);
} catch (e) {
    console.log('FATAL ' + e + ' | ' + (e && e.stack ? e.stack.split('\n').slice(1, 3).join(' <- ') : ''));
}
