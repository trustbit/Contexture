import * as d3 from 'd3';
import {fetchData} from "./DataAccess";

function calculateSizeFromHint(sizeHint) {
    const width = sizeHint.width;
    const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight)
    return {
        diameter: Math.max(Math.min(width, height), 1000)
    };
}

function initializeElements(element, {diameter}) {
    console.log('initializeElements')


    // Constants
    const width = diameter;
    const height = width;
    const innerRadius = Math.min(width, height) * 0.5 - 190;
    const outerRadius = innerRadius + 10;

    // Helpers
    const arc = d3.arc().innerRadius(innerRadius).outerRadius(outerRadius);

    const chord = d3
        .chordDirected()
        .padAngle(10 / innerRadius)
        .sortSubgroups(d3.descending)
        .sortChords(d3.descending);


    // Create SVG element
    const svg = d3
        .select(element)
        .append("svg")
        .attr("width", width)
        .attr("height", height)
        .attr("viewBox", [-width / 2, -height / 2, width, height]);

    const group = svg
        .append("g")
        .attr("font-size", 10)
        .attr("font-family", "sans-serif")
        .selectAll("g");

    const lines = svg
        .append("g")
        .attr("fill-opacity", 0.75)
        .selectAll("g")


    return {
        svg: svg,
        group: group,
        chord: chord,
        arc: arc,
        innerRadius: innerRadius,
        outerRadius: outerRadius,
        lines: lines
    };
}


function guessWidthAndHeightFromElement(element) {
    const parentStyle = window.getComputedStyle(element);
    const width =
        element.clientWidth
        - parseFloat(parentStyle.paddingLeft)
        - parseFloat(parentStyle.paddingRight);
    const maxHeight = Math.max(400,
        element.clientHeight
        - parseFloat(parentStyle.paddingTop)
        - parseFloat(parentStyle.paddingBottom))
    ;
    return {width: width, maxHeight: maxHeight};
}

// Lazily construct the package hierarchy from class names.
function packageMatrix(collaborations, domains, existIds) {
    function niceName(name) {
        return name.replaceAll(" ", "-");
    }

    function resolveDomainNames(domainId) {
        if (!domainId) {
            return [];
        }

        const domain = domains.find((domain) => domain.id == domainId);

        return [...resolveDomainNames(domain.parentDomainId), domain.key || domain.name]
            .filter((domainName) => domainName)
            .map(niceName);
    }

    function resolveCollaboratorName(collaborator) {
        const boundedContexts = domains
            .map((domain) => domain.boundedContexts)
            .flat();

        if (collaborator.boundedContext) {
            const boundedContext = boundedContexts.find(
                (boundedContext) => boundedContext.id == collaborator.boundedContext
            );

            if (!boundedContext) {
                throw new Error(
                    `Could not find a bounded context with id ${collaborator.boundedContext}`
                );
            }

            const domainNames = resolveDomainNames(
                boundedContext.parentDomainId
            ).join(".");

            return `${domainNames}.${boundedContext.key || boundedContext.name}`;
        }

        if (collaborator.domain) {
            const domain = domains.find(
                (domain) => domain.id == collaborator.domain
            );

            if (!domain) {
                throw new Error(
                    `Could not find a domain with id ${collaborator.domain}`
                );
            }

            const domainNames = resolveDomainNames(domain.parentDomainId).join(
                "."
            );

            return `${domainNames}.${domain.key || domain.name}`;
        }

        if (collaborator.externalSystem) {
            return `externalSystem.${collaborator.externalSystem}`;
        }

        if (collaborator.frontend) {
            return `frontend.${collaborator.frontend}`;
        }

        throw new Error(
            `Could not resolve a name for collaborator ${JSON.stringify(
                collaborator
            )}`
        );
    }

    function unique(names) {
        const obj = {};
        names.forEach((name) => (obj[name] = true));
        return Object.keys(obj);
    }


    const collaborationData = collaborations.map((collaboration) => {
        return {
            source: resolveCollaboratorName(collaboration.initiator),
            target: resolveCollaboratorName(collaboration.recipient),
            value: 1,
        };
    });

    // First step - ignore all domains/bounded contexts that do not define a collaboration
    const collaborationNames = unique(
        collaborations
            .map((collaboration) => [
                collaboration.initiator,
                collaboration.recipient,
            ])
            .flat()
            .map(resolveCollaboratorName)
    );

    collaborationNames.sort();

    function buildMatrix(data, names) {
        const index = new Map(names.map((name, i) => [name, i]));

        const matrix = Array.from(index, () => new Array(names.length).fill(0));
        for (const {source, target, value} of data) {
            matrix[index.get(source)][index.get(target)] += value;
        }

        return matrix;
    }


    let matrix = buildMatrix(collaborationData, collaborationNames);

    return {'matrix': matrix, 'names': collaborationNames};
}


// Create a class for the element
export class HierarchicalEdge extends HTMLElement {

    constructor() {
        // Always call super first in constructor
        super();

        this.resizeObserver = new ResizeObserver(entries => {
            const sizeHint = {width: entries[0].contentRect.width, maxHeight: entries[0].contentRect.height};
            this.resize(sizeHint)
        });

        this.shadow = this.attachShadow({mode: 'open'});
    }

    connectedCallback() {
        this.size = calculateSizeFromHint(guessWidthAndHeightFromElement(this.parentElement));
        this.rebuildHierarchicalEdgeElements();
        this.resizeObserver.observe(this.parentElement);
        this.buildHierarchicalEdge();
    }

    disconnectedCallback() {
        this.resizeObserver.disconnect();
    }

    attributeChangedCallback() {
        this.buildHierarchicalEdge();
    }

    static get observedAttributes() {
        return ['query', 'mode'];
    }

    inHighlightMode() {
        return this.getAttribute('mode') === 'highlighted';
    }

    async buildHierarchicalEdge() {
        const query = this.getAttribute('query');
        const baseApi = this.getAttribute('baseApi');

        this.data = await fetchData(baseApi, query, false);

        function find_children_ids(data) {
            let currentIds = []
            data.children.forEach(e => {
                let childIds = []
                if ('children' in e)
                    childIds = find_children_ids(e);
                currentIds.push(e.id)
                childIds.forEach(ce => currentIds.push(ce))
            })
            return currentIds;
        }

        this.existIds = find_children_ids(this.data)
        // console.log(existIds.length)

        const domainResponse = await fetch(`${baseApi}domains`);
        this.domains = await domainResponse.json();


        const collaborationResponse = await fetch(`${baseApi}collaborations`);
        this.collaborations = await collaborationResponse.json();

        function filteredColab(arr, existIds) {
            if (existIds.length == 0)
                return arr;

            let filteredArr = []
            for (let i = 0; i < arr.length; i++) {
                if (arr[i].initiator.boundedContext && existIds.includes(arr[i].initiator.boundedContext)) {
                    filteredArr.push(arr[i]);
                    continue;
                }

                if (arr[i].recipient.boundedContext && existIds.includes(arr[i].recipient.boundedContext)) {
                    filteredArr.push(arr[i]);
                    continue;
                }

                if (arr[i].initiator.domain && existIds.includes(arr[i].initiator.domain)) {
                    filteredArr.push(arr[i]);
                    continue;
                }

                if (arr[i].recipient.domain && existIds.includes(arr[i].recipient.domain)) {
                    filteredArr.push(arr[i]);
                    continue;
                }
            }

            return filteredArr;
        }

        this.collaborations = filteredColab(this.collaborations, this.existIds);

        this.rebuildHierarchicalEdgeElements();
        this.renderCollaborations();
    }

    resize(sizeHint) {
        this.size = calculateSizeFromHint(sizeHint);
        this.rebuildHierarchicalEdgeElements();
        this.renderCollaborations();
    }

    rebuildHierarchicalEdgeElements() {
        if (this.matrixEdge) {
            this.matrixEdge.svg.selectAll("*").remove();

            delete this.matrixEdge.chord;
            delete this.matrixEdge.arc;
            delete this.matrixEdge.group;
            delete this.matrixEdge.lines;
            delete this.matrixEdge.svg;
            delete this.matrixEdge;
        }
        this.shadow.innerHTML = '';

        this.matrixEdge = initializeElements(this.shadow, this.size);
    }

    renderCollaborations() {
        if (this.collaborations === undefined || this.domains === undefined)
            return;

        let formattedData = packageMatrix(this.collaborations, this.domains, this.existIds)

        let matrix = formattedData['matrix']
        let names = formattedData['names']

        const color = d3.scaleOrdinal(
            names,
            d3.quantize(d3.interpolateRainbow, names.length)
        );

        const chords = this.matrixEdge.chord(matrix);
        const indexDict = {}
        for (let i = 0; i < chords.length; i++) {
            if (!(chords[i].source.index in indexDict)) {
                indexDict[chords[i].source.index] = ''
            }
            indexDict[chords[i].source.index] += chords[i].target.index + ','
        }

        const group = this.matrixEdge.group
            .data(chords.groups)
            .join("g");

        const rootNode = this.matrixEdge.svg;
        let mouseoveredFn = this.mouseovered;
        let mouseoutedFn = this.mouseouted;

        group
            .append("path")
            .attr("fill", (d) => color(names[d.index]))
            .attr("d", this.matrixEdge.arc)
            .attr("class", d => "group")
            .attr("index", d => d.index)
            .attr("targetIndex", d => d.index in indexDict ? indexDict[d.index] : '')
            .on("mouseover", function (d) {
                mouseoveredFn(rootNode, d)
            })
            .on("mouseout", function (d) {
                mouseoutedFn(rootNode, d)
            });


        group
            .append("text")
            .each((d) => (d.angle = (d.startAngle + d.endAngle) / 2))
            .attr("class", d => "group")
            .attr("index", d => d.index)
            .attr("targetIndex", d => d.index in indexDict ? indexDict[d.index] : '')
            .attr("dy", "0.35em")
            .attr(
                "transform",
                (d) => `
            rotate(${(d.angle * 180) / Math.PI - 90})
            translate(${this.matrixEdge.outerRadius + 5})
            ${d.angle > Math.PI ? "rotate(180)" : ""}
          `
            )
            .attr("text-anchor", (d) => (d.angle > Math.PI ? "end" : null))
            .text((d) => names[d.index])
            .on("mouseover", function (d) {
                mouseoveredFn(rootNode, d)
            })
            .on("mouseout", function (d) {
                mouseoutedFn(rootNode, d)
            });

        group.append("title").text(
            (d) => `${names[d.index]}
    ${d3.sum(
                chords,
                (c) => (c.source.index === d.index) * c.source.value
            )} outgoing
    ${d3.sum(
                chords,
                (c) => (c.target.index === d.index) * c.source.value
            )} incoming `
        );

        const ribbon = d3
            .ribbonArrow()
            .radius(this.matrixEdge.innerRadius - 1)
            .padAngle(1 / this.matrixEdge.innerRadius);

        this.matrixEdge.lines
            .data(chords)
            .join("path")
            .style("mix-blend-mode", "multiply")
            .attr("fill", (d) => color(names[d.target.index]))
            .attr("d", ribbon)
            .attr("class", d => "chord")
            .attr("sourceIndex", d => d.source.index)
            .attr("targetIndex", d => d.target.index)
            .append("title")
            .text(
                (d) =>
                    `${names[d.source.index]} → ${names[d.target.index]} ${
                        d.source.value
                        }`
            )

        ;
    }


    mouseovered(rootNode, d) {
        const index = d.currentTarget.attributes.index.value;
        const targetIndex = d.currentTarget.attributes.targetIndex.value.split(',');
        const targetIndexes = targetIndex.map(function (x) {
            return parseInt(x, 0);
        });

        console.log(index)

        rootNode.selectAll(".chord").style("opacity", .1)
        rootNode.selectAll(`.chord[sourceIndex="${index}"]`).style("opacity", 1)

        rootNode.selectAll("text.group").style("opacity", .1)
        rootNode.selectAll(`text.group[index="${index}"]`).style("opacity", 1)
        targetIndexes.forEach(e => rootNode.selectAll(`text.group[index="${e}"]`).style("opacity", 1))
    }

    mouseouted(rootNode, d) {
        rootNode.selectAll(".chord").style("opacity", 0.75)
        rootNode.selectAll("text.group").style("opacity", 0.75)
    }
}

// customElements.define('hierarchical-edge', HierarchicalEdge);