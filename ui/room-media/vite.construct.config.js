import {defineConfig} from 'vite';
export default defineConfig({build:{outDir:'construct-assets',emptyOutDir:true,lib:{entry:'construct-main.js',name:'RoomMediaConstructBundle',formats:['iife'],fileName:()=> 'construct-room.js'}}});
