// Probe 4: document list + the shape/command surface I would need to author art.
const app = require('/application.js').app;

const docs = app.documents();
console.log('documentCount=' + (docs ? docs.length : 'null'));
if (docs) {
    for (let i = 0; i < docs.length; i++) {
        const d = docs[i];
        let spreads = '?';
        try { spreads = d.spreads.length; } catch (e) { spreads = 'ERR'; }
        console.log('  doc[' + i + '] name=' + d.name + ' uuid=' + d.sessionUuid +
                    ' spreads=' + spreads + ' width=' + d.width + ' height=' + d.height);
    }
}

console.log('desktop=' + app.getUserDesktopPath());
console.log('version=' + app.version + ' platform=' + app.platformName + ' uiParadigm=' + app.uiParadigm);

const doc = require('/document.js');
console.log('NewDocumentOptions: ' + Object.getOwnPropertyNames(doc.NewDocumentOptions.prototype || doc.NewDocumentOptions).join(','));
console.log('DocumentExportRecord: ' + Object.getOwnPropertyNames(doc.DocumentExportRecord.prototype || doc.DocumentExportRecord).join(','));
console.log('FileExportOptions: ' + Object.getOwnPropertyNames(doc.FileExportOptions.prototype || doc.FileExportOptions).join(','));
console.log('Document proto: ' + Object.getOwnPropertyNames(doc.Document.prototype).join(','));
console.log('DocumentPromises: ' + Object.getOwnPropertyNames(doc.DocumentPromises).join(','));
