// ATM wee need to full name reference so the build succeeds
import {Sunburst} from "../Visualizations/Sunburst/Index.js";

export function searchPorts(app) {
    if (app && app.ports) {
        if (app.ports.storePresentation && app.ports.onPresentationChanged) {
            app.ports.storePresentation.subscribe(function (mode) {
                localStorage.setItem("search_presentation", mode);
            });

            const searchPresentation = localStorage.getItem('search_presentation')
            app.ports.onPresentationChanged.send(searchPresentation || "unknown")
        }

        if (app.ports.changeQueryString && app.ports.onQueryStringChanged) {
            // see https://github.com/elm/browser/blob/1.0.0/notes/navigation-in-elements.md

            function extractSearchParams(queryString) {
                const params = new URLSearchParams(queryString);
                return Array
                    .from(params.entries())
                    .map(([key, value]) => {
                        return {"name": key, "value": value};
                    });
            }

            function asSearchParams(values) {
                const params = new URLSearchParams();
                for (const value of values) {
                    params.append(value.name, value.value);
                }
                return params.toString();
            }

            function notifyQueryStringChange() {
                app.ports.onQueryStringChanged.send(JSON.stringify(extractSearchParams(location.search)));
            }

            // Inform app of browser navigation (the BACK and FORWARD buttons)
            window.addEventListener('popstate', function (event) {
                notifyQueryStringChange();
            });

            // Change the URL upon request, inform app of the change.
            app.ports.changeQueryString.subscribe(function (queryParameters) {
                const queryString = asSearchParams(JSON.parse(queryParameters));
                if (new URLSearchParams(location.search).toString() !== queryString) {
                    history.pushState({}, '', queryString.startsWith("?") ? queryString : "?" + queryString);
                    notifyQueryStringChange();
                }
            });

            // send initial query string to application
            notifyQueryStringChange();
        }


        customElements.define('visualization-sunburst', Sunburst);
    }
}