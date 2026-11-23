// Probe 5: the ApplicationDocuments class, settings, and how to reach an open document.
const mod = require('/application.js');
const app = mod.app;

console.log('ApplicationDocuments proto: ' +
    Object.getOwnPropertyNames(mod.ApplicationDocuments ? mod.ApplicationDocuments.prototype : app.documents.prototype).join(','));
console.log('mod exports: ' + Object.getOwnPropertyNames(mod).join(','));

try {
    const docs = new app.documents();
    console.log('new documents() len=' + docs.length);
    for (let i = 0; i < docs.length; i++) {
        const d = docs[i];
        console.log('  doc[' + i + '] name=' + d.name + ' uuid=' + d.sessionUuid);
    }
} catch (e) {
    console.log('new documents() ERR ' + e);
}

try {
    console.log('settings props: ' + Object.getOwnPropertyNames(app.settings).join(','));
    console.log('settings json: ' + JSON.stringify(app.settings));
} catch (e) {
    console.log('settings ERR ' + e);
}

console.log('desktop=' + app.getUserDesktopPath());
console.log('resources=' + app.resourcesPath);
console.log('version=' + app.version + ' build=' + app.buildVersion + ' platform=' + app.platformName);
