// Probe: what can this Affinity session see right now?
const app = require('/application.js');

console.log('app keys=' + Object.getOwnPropertyNames(Object.getPrototypeOf(app)).join(','));

const docs = app.documents;
console.log('documentCount=' + docs.length);
for (let i = 0; i < docs.length; i++) {
    const d = docs[i];
    console.log('doc[' + i + '] name=' + d.name + ' uuid=' + d.sessionUuid);
}
