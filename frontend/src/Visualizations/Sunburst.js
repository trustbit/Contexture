export class Sunburst extends HTMLElement {
    // things required by Custom Elements
    constructor() {
        super();
    }

    connectedCallback() {
        this.drawSunburst();
    }

    attributeChangedCallback() {
        this.drawSunburst();
    }

    static get observedAttributes() {
        return ['query', 'baseApi'];
    }
    
    baseApi() {
        return this.getAttribute('baseApi');
    }

    findRelevantDomainIds(
        domainDictionary,
        boundedContextsDictionary
    ) {
        let relevantDomainsIds = new Set(Object.keys(boundedContextsDictionary));
        // starting from the domains of the matched bounded contexts
        // we need to select all domains relevant for the visualization
        // this is done in iterations, because we we might have the following situation:
        // Found Bounded Context -> SubDomain -> SubDomain -> Domain
        // ATM we can only resolve the next parent domain, so a while loop is needed until we reach a fix-point.
        let newDomainIds = [];
        do {
            newDomainIds = Array.from(relevantDomainsIds.values())
                .map((domainId) => {
                    const domain = domainDictionary[domainId];
                    if (
                        domain.parentDomainId &&
                        !relevantDomainsIds.has(domain.parentDomainId)
                    )
                        return domain.parentDomainId;
                })
                .filter((d) => !!d);
            newDomainIds.forEach((domainId) => relevantDomainsIds.add(domainId));
        } while (newDomainIds.length > 0);
        return relevantDomainsIds;
    }
    async fetchDomains() {
        const response = await fetch($`${this.baseApi()}/api/domains`);

        return await response.json();
    }
    async fetchBoundedContexts(query) {
        const response = await fetch(
            `${this.baseApi()}/api/boundedContexts${query}`
        );

        return await response.json();
    }
    async drawSunburst() {
        const query = this.getAttribute('query');
        document.getElementById("title").innerText = query;

        const contexts = await this.fetchBoundedContexts(query);
        const domains = await this.fetchDomains();

        const domainDictionary = Array.from(domains).reduce((dict, d) => {
            dict[d.id] = d;
            return dict;
        }, {});
        const boundedContextsDictionary = Array.from(contexts).reduce(
            (dict, bc) => {
                if (!dict[bc.parentDomainId]) dict[bc.parentDomainId] = [];
                dict[bc.parentDomainId].push(bc);
                return dict;
            },
            {}
        );

        const relevantDomainsIds = findRelevantDomainIds(
            domainDictionary,
            boundedContextsDictionary
        );
        function isRelevantDomain(domain) {
            return relevantDomainsIds.has(domain.id);
        }

        function mapDomain(domain) {
            // domain.boundedContexts is not filled in subdomains of the domain
            const boundedContexts = boundedContextsDictionary[domain.id] || [];
            return {
                name: domain.name,
                children: [
                    ...domain.subdomains.filter(isRelevantDomain).map(mapDomain),
                    ...boundedContexts.map(mapBoundedContext),
                ],
            };
        }

        function mapBoundedContext(boundedContext) {
            return {
                name: `[BCC] ${boundedContext.name}`,
                children: [],
            };
        }

        const data = {
            name: "root",
            children: domains
                .filter((domain) => !domain.parentDomainId)
                .filter(isRelevantDomain)
                .map(mapDomain),
        };

        renderDomains(data);
    }
    
    renderDomains(data) {
        // Constants
        const width = 975;
        const height = 975;

        const radius = width / 2;

        // Helpers
        function partition(data) {
            console.log(d3.partition().size([2 * Math.PI, radius]));

            // TODO: Weird error in partitioning
            const partitioned = d3.partition().size([2 * Math.PI, radius])(
                d3
                    .hierarchy(data)
                    .sum((d) => (d.children.length >= 1 ? 0 : 1))
                    .sort((a, b) => b.value - a.value)
            );

            console.log(partitioned);
            return partitioned;
        }

        const color = d3.scaleOrdinal(
            d3.quantize(d3.interpolateRainbow, data.children.length + 1)
        );

        const arc = d3
            .arc()
            .startAngle((d) => d.x0)
            .endAngle((d) => d.x1)
            .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.005))
            .padRadius(radius / 2)
            .innerRadius((d) => d.y0)
            .outerRadius((d) => d.y1 - 1);

        const format = d3.format(",d");

        // Building
        const root = partition(data);

        //Create SVG element
        const svg = d3
            .select("body")
            .append("svg")
            .attr("width", width)
            .attr("height", height)
            .attr("viewBox", `0 0 ${width} ${height}`);

        svg
            .append("g")
            .attr("transform", `translate(${width / 2} ${height / 2})`)
            .attr("fill-opacity", 0.6)
            .selectAll("path")
            .data(root.descendants().filter((d) => d.depth))
            .join("path")
            .attr("fill", (d) => {
                while (d.depth > 1) d = d.parent;
                return color(d.data.name);
            })
            .attr("d", arc)
            .append("title")
            .text(
                (d) =>
                    `${d
                        .ancestors()
                        .map((d) => d.data.name)
                        .reverse()
                        .join("/")}\n${format(d.value)} Elements`
            );

        svg
            .append("g")
            .attr("transform", `translate(${width / 2} ${height / 2})`)
            .attr("pointer-events", "none")
            .attr("text-anchor", "middle")
            .attr("font-size", 10)
            .attr("font-family", "sans-serif")
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
            .text((d) => d.data.name);
    }
}