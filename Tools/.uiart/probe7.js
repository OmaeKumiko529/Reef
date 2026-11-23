// Probe 7: how to reach the current document, and the arity of the calls I will need.
const app = require('/application.js').app;
const docMod = require('/document.js');
const cmdMod = require('/commands.js');

function arity(path, fn) { console.log(path + ' arity=' + (typeof fn === 'function' ? fn.length : typeof fn)); }

try { console.log('documents.all typeof=' + typeof app.documents.all); } catch (e) { console.log('all ERR ' + e); }
try {
    const all = app.documents.all;
    console.log('all len=' + (all && all.length));
    if (all) for (let i = 0; i < all.length; i++) console.log('  all[' + i + '] ' + all[i].name + ' ' + all[i].sessionUuid);
} catch (e) { console.log('all ERR ' + e); }
try {
    const cur = app.documents.current;
    console.log('current=' + (cur ? (cur.name + ' uuid=' + cur.sessionUuid + ' w=' + cur.widthPixels + ' h=' + cur.heightPixels + ' spreads=' + cur.spreads.length) : 'none'));
} catch (e) { console.log('current ERR ' + e); }

arity('Document.create', docMod.Document.create);
arity('Document.createAsync', docMod.Document.createAsync);
arity('Document.getCurrentAsync', docMod.Document.getCurrentAsync);
arity('DocumentPromises.create', docMod.DocumentPromises.create);
arity('NewDocumentOptions ctor', docMod.NewDocumentOptions);
arity('Document.export', docMod.Document.prototype.export);
arity('Document.exportAsync', docMod.Document.prototype.exportAsync);
arity('Document.executeCommand', docMod.Document.prototype.executeCommand);
arity('Document.addNode', docMod.Document.prototype.addNode);
arity('Document.rootNode?', 1);

arity('DocumentCommand.createAddCurve', cmdMod.DocumentCommand.createAddCurve);
arity('DocumentCommand.createSetText', cmdMod.DocumentCommand.createSetText);
arity('DocumentCommand.createSetShape', cmdMod.DocumentCommand.createSetShape);
arity('DocumentCommand.createSetPenFill', cmdMod.DocumentCommand.createSetPenFill);
arity('DocumentCommand.createSetLineStyle', cmdMod.DocumentCommand.createSetLineStyle);
arity('DocumentCommand.createAddArtboard', cmdMod.DocumentCommand.createAddArtboard);
arity('DocumentCommand.createSetExportConfig', cmdMod.DocumentCommand.createSetExportConfig);
arity('DocumentCommand.createGenerateImage', cmdMod.DocumentCommand.createGenerateImage);
arity('DocumentCommand.createSetAddNoiseFilterParameters', cmdMod.DocumentCommand.createSetAddNoiseFilterParameters);
arity('AddChildNodesCommandBuilder', cmdMod.AddChildNodesCommandBuilder);
arity('AddChildNodesCommandBuilder proto', cmdMod.AddChildNodesCommandBuilder.prototype);

const sel = require('/selections.js');
console.log('selections module: ' + Object.getOwnPropertyNames(sel).join(','));
if (sel.Selection) {
    arity('Selection.create', sel.Selection.create);
    console.log('Selection static: ' + Object.getOwnPropertyNames(sel.Selection).join(','));
}
