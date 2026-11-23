// Probe 10: rectangle struct, line style setters, story builder, export config.
function names(o) { try { return Object.getOwnPropertyNames(o); } catch (e) { return ['ERR:' + e]; } }
const geometryModule = require('affinity:geometry');
const coloursModule = require('affinity:colours');

const rect = new geometryModule.Rectangle();
console.log('Rectangle props: ' + names(rect).join(',') + ' json=' + JSON.stringify(rect));
try { rect.x = 1; rect.y = 2; rect.width = 3; rect.height = 4; console.log('rect after set=' + JSON.stringify(rect)); } catch (e) { console.log('rect set ERR ' + e); }

const ls = require('/linestyle.js');
const style = ls.LineStyle.createDefault();
console.log('LineStyle proto: ' + names(Object.getPrototypeOf(style)).join(','));
console.log('LineCap: ' + names(ls.LineCap).join(','));
console.log('LineJoin: ' + names(ls.LineJoin).join(','));
console.log('StrokeAlignment: ' + names(ls.StrokeAlignment).join(','));

const story = require('/story.js');
console.log('story exports: ' + names(story).join(','));
if (story.StoryBuilder) {
    console.log('StoryBuilder statics: ' + names(story.StoryBuilder).join(','));
    console.log('StoryBuilder proto: ' + names(story.StoryBuilder.prototype).join(','));
}

const docMod = require('/document.js');
console.log('FileExportArea: ' + names(docMod.FileExportArea).join(','));
console.log('FileExportOptions statics: ' + names(docMod.FileExportOptions).join(',') + ' proto: ' + names(docMod.FileExportOptions.prototype).join(','));
console.log('ImagePlacement: ' + names(docMod.ImagePlacement).join(','));

const exp = require('/exportconfig.js');
console.log('exportconfig exports: ' + names(exp).join(','));
if (exp.ExportConfig) console.log('ExportConfig statics: ' + names(exp.ExportConfig).join(',') + ' proto: ' + names(exp.ExportConfig.prototype).join(','));

const fs = require('/fs.js');
console.log('fs exports: ' + names(fs).join(','));
