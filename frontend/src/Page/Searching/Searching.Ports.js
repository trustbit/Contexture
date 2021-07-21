// ATM wee need to full name reference so the build succeeds
import {Sunburst} from "../../Visualizations/Sunburst/Index.js";

export function searchingPorts(app) {
    if (app && app.ports && app.ports.storePresentation) {
        app.ports.storePresentation.subscribe(function (mode) {
            document.cookie = 'search_presentation=' + mode + ';max-age=31536000';
        });

        customElements.define('visualization-sunburst', Sunburst);
    }
}