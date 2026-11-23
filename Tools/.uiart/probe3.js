// Probe 3: the application singleton (module exports the class Application, a BuildKind, a
// UiParadigm and the `app` singleton itself).
const mod = require('/application.js');
const app = mod.app;

console.log('mod.app typeof=' + typeof app);
console.log('app props: ' + Object.getOwnPropertyNames(app).join(','));
console.log('app proto props: ' + Object.getOwnPropertyNames(Object.getPrototypeOf(app)).join(','));

function dump(label, obj) {
    try {
        console.log(label + ' -> ' + Object.getOwnPropertyNames(obj).join(','));
    } catch (e) {
        console.log(label + ' -> ERR ' + e);
    }
}
dump('Application proto', mod.Application.prototype);

try {
    const docs = app.documents;
    console.log('documents typeof=' + typeof docs + ' len=' + (docs && docs.length));
    if (docs && docs.length) {
        for (let i = 0; i < docs.length; i++) {
            const d = docs[i];
            console.log('  doc[' + i + '] name=' + d.name + ' uuid=' + d.sessionUuid);
        }
    }
} catch (e) {
    console.log('documents ERR ' + e);
}
