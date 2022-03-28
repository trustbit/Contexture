// ATM wee need to full name reference so the build succeeds
import {Bubble} from "../Visualizations/Bubble.js";

export function mainPorts(app) {
    if (app && app.ports) {
        customElements.define('bubble-visualization', Bubble);
        app.ports.showHome.subscribe(function () {
            const bubble = document.querySelector('bubble-visualization');
            bubble.showMain();
        });
        app.ports.showAllConnections.subscribe(function (flag) {
            const bubble = document.querySelector('bubble-visualization');
            bubble.showAllConnections(flag);
        });

        if (app.ports.storeVisualization && app.ports.onVisualizationChanged) {
            app.ports.storeVisualization.subscribe(function (mode) {
                localStorage.setItem("domainIndex_visualization", mode);
            });

            const vizualization = localStorage.getItem('domainIndex_visualization')
            app.ports.onVisualizationChanged.send(vizualization || "unknown")
        }
    }
}