// Probe 6: locate the document handle and map the command surface.
const mod = require('/application.js');
const app = mod.app;
const docMod = require('/document.js');
const cmdMod = require('/commands.js');

function names(o) { try { return Object.getOwnPropertyNames(o); } catch (e) { return ['ERR:' + e]; } }

console.log('== ApplicationDocuments class ==');
console.log(names(app.documents).join(','));
try {
    const inst = new app.documents();
    console.log('instance own: ' + names(inst).join(','));
    console.log('instance count=' + inst.count + ' length=' + inst.length);
    console.log('item(0)=' + (inst.item ? String(inst.item(0)) : 'no item()'));
} catch (e) { console.log('ERR ' + e); }

console.log('== Document class ==');
const dn = names(docMod.Document);
console.log(dn.filter(function (n) { return /^(create|load|open|new|get|from)/i.test(n) || /promise/i.test(n); }).join(','));
console.log('Document.prototype: ' + names(docMod.Document.prototype).join(','));

console.log('== NewDocumentOptions ==');
console.log(names(docMod.NewDocumentOptions.prototype || docMod.NewDocumentOptions).join(','));
console.log('== DocumentPromises ==');
console.log(names(docMod.DocumentPromises).join(','));

console.log('== DocumentCommand (filtered) ==');
const all = names(cmdMod.DocumentCommand);
console.log('total=' + all.length);
console.log(all.filter(function (n) { return /create|Export|Text|Shape|Curve|Vector|Fill|Stroke|Layer|Add|Insert|Selection/i.test(n); }).join(','));
