const fs=require('fs');
const path=require('path');
const project=path.resolve(__dirname,'..');
fs.copyFileSync(path.join(project,'dist','live.html'),path.join(project,'dist','index.html'));
console.log('Live entry copied to dist/index.html. Verify join-map.enabled and wiring before deployment.');
