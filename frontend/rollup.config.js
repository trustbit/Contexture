import multi from '@rollup/plugin-multi-entry';
import { nodeResolve } from '@rollup/plugin-node-resolve';

export default {
    input: 'src/**/*.js',
    output: {
        file: 'assets/js/Contexture-Addons.js',
        format: 'iife',
        name: 'Contexture'        
    },
    // external:['d3'],
    plugins: [multi(),nodeResolve()]
};