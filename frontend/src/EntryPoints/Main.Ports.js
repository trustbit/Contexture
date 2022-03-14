// ATM wee need to full name reference so the build succeeds
import {Bubble} from "../Visualizations/Bubble.js";

export function mainPorts(app) {
    if (app) {
        customElements.define('bubble-visualization', Bubble);
    }
}