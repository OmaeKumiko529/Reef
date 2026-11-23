// Probe 8: create a document sized for a seal stamp, then introspect the shape/text API.
const app = require('/application.js').app;
const docMod = require('/document.js');
const cmdMod = require('/commands.js');

function names(o) { try { return Object.getOwnPropertyNames(o); } catch (e) { return ['ERR:' + e]; } }
function arity(label, fn) { console.log(label + ' arity=' + (typeof fn === 'function' ? fn.length : typeof fn)); }

// what units enum values exist?
console.log('UnitType: ' + names(docMod.UnitType).join(','));
try { console.log('UnitType.Pixels.value=' + docMod.UnitType.Pixels.value + ' keys=' + docMod.UnitType.keys); } catch (e) {}

const opts = new docMod.NewDocumentOptions();
console.log('opts own: ' + names(opts).join(','));
opts.width = 176;
opts.height = 72;
opts.dpi = 96;
opts.units = docMod.UnitType.Pixels;
opts.isTransparentBackground = true;
opts.createArtboard = false;
opts.pageCount = 1;

let doc = null;
try {
    doc = docMod.Document.create(opts);
    console.log('create -> ' + (doc ? 'ok' : 'null'));
} catch (e) {
    console.log('create ERR ' + e);
}
if (!doc) {
    try {
        doc = docMod.Document.createAsync(opts, function (d) { console.log('async cb doc=' + (d ? d.name : 'null')); });
        console.log('createAsync -> ' + (doc ? 'pending/ok' : 'null'));
    } catch (e) { console.log('createAsync ERR ' + e); }
}
if (doc) {
    console.log('doc name=' + doc.name + ' uuid=' + doc.sessionUuid + ' px=' + doc.widthPixels + 'x' + doc.heightPixels +
                ' spreads=' + doc.spreads.length + ' units=' + doc.units);
    console.log('rootNode: ' + names(doc.rootNode).join(','));
}

const shapes = require('/shapes.js');
console.log('shapes module: ' + names(shapes).join(','));
for (const k of names(shapes)) {
    const v = shapes[k];
    if (typeof v === 'function') console.log('  shapes.' + k + ' arity=' + v.length + ' static=' + names(v).join('/'));
}

const story = require('/story.js');
console.log('story module: ' + names(story).slice(0, 40).join(','));

console.log('Command.execute: ' + names(cmdMod.Command.prototype).join(','));
arity('createCompoundCommand', cmdMod.createCompoundCommand);
console.log('doc.executeCommand arity=' + docMod.Document.prototype.executeCommand.length);
