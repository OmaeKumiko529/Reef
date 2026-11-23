// Probe 9: native struct constructors (colour + geometry) and the line style surface.
function names(o) { try { return Object.getOwnPropertyNames(o); } catch (e) { return ['ERR:' + e]; } }

const coloursModule = require('affinity:colours');
console.log('affinity:colours keys: ' + names(coloursModule).join(','));

const geometryModule = require('affinity:geometry');
console.log('affinity:geometry keys: ' + names(geometryModule).join(','));

function tryStruct(mod, name, fields) {
    try {
        const Ctor = mod[name];
        if (typeof Ctor !== 'function') { console.log(name + ' is ' + typeof Ctor); return null; }
        const inst = new Ctor();
        console.log(name + ' instance props: ' + names(inst).join(','));
        if (fields) for (const f of fields) { try { inst[f] = 1; } catch (e) { console.log('  set ' + f + ' ERR ' + e); } }
        console.log(name + ' json=' + JSON.stringify(inst));
        return inst;
    } catch (e) {
        console.log(name + ' ERR ' + e);
        return null;
    }
}

const rgba = tryStruct(coloursModule, 'RGBA8');
if (rgba) {
    try {
        rgba.r = 176; rgba.g = 48; rgba.b = 24; rgba.a = 255;
        console.log('RGBA8 set -> ' + JSON.stringify(rgba));
        const col = require('/colours.js').Colour.createRGBA8(rgba);
        console.log('Colour.createRGBA8 ok -> ' + String(col));
    } catch (e) { console.log('RGBA8 assign ERR ' + e); }
}

for (const n of ['Rect', 'RectF', 'Point', 'Size', 'BoundingBox', 'Transform', 'Matrix']) {
    if (geometryModule[n]) tryStruct(geometryModule, n);
}

const ls = require('/linestyle.js');
console.log('linestyle exports: ' + names(ls).join(','));
if (ls.LineStyle) {
    console.log('LineStyle statics: ' + names(ls.LineStyle).join(','));
    try {
        const style = ls.LineStyle.createDefault();
        console.log('LineStyle instance: ' + names(style).join(','));
    } catch (e) { console.log('LineStyle.createDefault ERR ' + e); }
}
