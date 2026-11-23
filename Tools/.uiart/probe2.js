// Probe 2: introspect the native application object and the document module.
const app = require('/application.js');
console.log('application own props: ' + Object.getOwnPropertyNames(app).join(','));
console.log('application typeof props: ' + Object.getOwnPropertyNames(app)
    .map(function (k) { try { return k + ':' + typeof app[k]; } catch (e) { return k + ':ERR'; } }).join(' | '));

const doc = require('/document.js');
console.log('document module props: ' + Object.getOwnPropertyNames(doc).join(','));

const cmd = require('/commands.js');
console.log('commands module props: ' + Object.getOwnPropertyNames(cmd).join(','));
