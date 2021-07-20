import {Sunburst} from "../../Visualizations/Sunburst";

export function searchingPorts(app) {
    if (app && app.ports && app.ports.storePresentation) {
        app.ports.storePresentation.subscribe(function (mode) {
            document.cookie = 'search_presentation=' + mode + ';max-age=31536000';
        });

        customElements.define('visualization-sunburst', Sunburst);
    }
}