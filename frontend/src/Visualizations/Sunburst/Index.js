import * as d3 from 'd3';
import {fetchData} from "./DataAccess";

function partition(data, radius) {
    // TODO: Weird error in partitioning
    const partitioned = d3.partition().size([2 * Math.PI, radius])(
        d3
            .hierarchy(data || {children: []})
            .sum((d) => (d.children.length >= 1 ? 0 : 1))
            .sort((a, b) => b.value - a.value)
    );

    return partitioned;
}

function initializeElements(element, {width, height}) {
    //Create the d3 elements
    const svg = d3
        .select(element)
        .append("svg")
        .attr("width", width)
        .attr("height", height)
        .attr("viewBox", `0 0 ${width} ${height}`)

    const elements =
        svg
            .append("g")
            .attr("transform", `translate(${width / 2} ${height / 2})`)
            .attr("fill-opacity", 0.6);
    const text =
        svg
            .append("g")
            .attr("transform", `translate(${width / 2} ${height / 2})`)
            .attr("pointer-events", "none")
            .attr("text-anchor", "middle")
            .attr("font-size", 10)
            .attr("font-family", "sans-serif");
    return {svg: svg, elements: elements, text: text};
}

function guessWidthAndHeightFromElement(element) {
    const parentStyle = window.getComputedStyle(element);
    const width =
        element.clientWidth
        - parseFloat(parentStyle.paddingLeft)
        - parseFloat(parentStyle.paddingRight);
    const maxHeight =
        element.clientHeight
        - parseFloat(parentStyle.paddingTop)
        - parseFloat(parentStyle.paddingBottom);
    return {width: width, maxHeight: maxHeight};
}

function calculateSizeFromHint(sizeHint) {
    const width = sizeHint.width;
    const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight)
    return {
        width: width,
        height: height,
        radius: Math.min(width, height) / 2
    };
}

function shortenText(text, width) {
    text.each(function () {
        const text = d3.select(this)
        let content = text.text();
        while (text.node().getComputedTextLength() > width) {
            content = content.substr(0, content.length - 4) + "..."
            text.text(content)
        }
    });
}

export class Sunburst extends HTMLElement {
    // things required by Custom Elements
    constructor() {
        super();
        this.resizeObserver = new ResizeObserver(entries => {
            const sizeHint = {width: entries[0].contentRect.width, maxHeight: entries[0].contentRect.height};
            this.resize(sizeHint)
        });
    }

    connectedCallback() {
        this.size = calculateSizeFromHint(guessWidthAndHeightFromElement(this.parentElement));
        this.rebuildSunburstElements();
        this.resizeObserver.observe(this.parentElement);
        this.buildSunburst();
    }

    disconnectedCallback() {
        this.resizeObserver.disconnect();
    }

    attributeChangedCallback() {
        this.buildSunburst();
    }

    static get observedAttributes() {
        return ['query', 'mode'];
    }

    inHighlightMode() {
        return this.getAttribute('mode') === 'highlighted';
    }

    async buildSunburst() {
        const query = this.getAttribute('query');
        const baseApi = this.getAttribute('baseApi');

        this.data = await fetchData(baseApi, query, this.inHighlightMode());

        this.renderSunburst();
    }

    resize(sizeHint) {
        this.size = calculateSizeFromHint(sizeHint);
        this.rebuildSunburstElements();
        this.renderSunburst();
    }

    rebuildSunburstElements() {
        if (this.sunburst) {
            const node = this.sunburst.svg.node();
            if (node)
                this.removeChild(node);
            delete this.sunburst.elements;
            delete this.sunburst.text
            delete this.sunburst.svg;
            delete this.sunburst;
        }
        this.sunburst = initializeElements(this, this.size);
    }

    renderSunburst() {
        const arc = d3
            .arc()
            .startAngle((d) => d.x0)
            .endAngle((d) => d.x1)
            .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.005))
            .padRadius(this.size.radius / 2)
            .innerRadius((d) => d.y0)
            .outerRadius((d) => d.y1 - 1);

        const format = d3.format(",d");

        // Building
        const root = partition(this.data, this.size.radius);

        const color = d3.scaleOrdinal(
            d3.quantize(d3.interpolateRainbow, root.children ? root.children.length + 1 : 0)
        );

        const inHighlightMode = this.inHighlightMode();

        function highlightOpacity(d) {
            if (inHighlightMode) {
                if (d.data.wasFound) {
                    return d.data.isBoundedContext
                        ? 1.0
                        : 0.7
                } else {
                    return 0.3;
                }
            } else {
                return d.data.isBoundedContext
                    ? 1.0
                    : 0.7;
            }
        }

        this.sunburst.elements
            .selectAll("path")
            .data(root.descendants().filter((d) => d.depth))
            .join("path")
            .attr("fill", (d) => {
                while (d.depth > 1) d = d.parent;
                return color(d.data.name);
            })
            .attr("opacity", highlightOpacity)
            .attr("d", arc)
            .append("title")
            .text(
                (d) => {
                    const ancestors =
                        d
                            .ancestors()
                            .reverse()
                            .splice(1) // ignore 'root' as name
                            .map((d) => d.data.name)
                            .join("/");
                    if (d.data.isBoundedContext) {
                        return `Bounded Context\n${ancestors}`;
                    } else {
                        return `${ancestors}:\n${format(d.value)} Elements`;
                    }
                }
            );

        this.sunburst.text
            .selectAll("text")
            .data(
                root
                    .descendants()
                    .filter((d) => d.depth && ((d.y0 + d.y1) / 2) * (d.x1 - d.x0) > 10)
            )
            .join("text")
            .attr("transform", function (d) {
                const x = (((d.x0 + d.x1) / 2) * 180) / Math.PI;
                const y = (d.y0 + d.y1) / 2;
                return `rotate(${
                    x - 90
                }) translate(${y},0) rotate(${x < 180 ? 0 : 180})`;
            })
            .attr("dy", "0.35em")
            .attr("opacity", highlightOpacity)
            .text((d) => d.data.name)
            .call(shortenText, Math.ceil(this.size.radius / (root.height + 1)));
    }
}