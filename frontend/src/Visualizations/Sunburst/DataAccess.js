function findRelevantDomainIds(
    domainDictionary,
    relevantDomainsIds
) {
    // we need to select all domains relevant for the visualization
    // this is done in iterations, because we might have the following situation:
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

async function fetchDomains(baseApi) {
    const response = await fetch(`${baseApi}domains`);

    return await response.json();
}

async function fetchBoundedContexts(baseApi, query) {
    const response = await fetch(
        `${baseApi}boundedContexts${query}`
    );

    return await response.json();
}

export async function fetchData(baseApi, query, highlightMode) {
    const filteredContexts = await fetchBoundedContexts(baseApi, query);
    const allContexts = await fetchBoundedContexts(baseApi);
    const domains = await fetchDomains(baseApi);

    const domainDictionary = Array
        .from(domains)
        .reduce((dict, d) => {
            dict[d.id] = d;
            return dict;
        }, {});
    const boundedContextsToDisplay = Array
        .from(highlightMode ? allContexts : filteredContexts)
        .reduce(
            (dict, bc) => {
                if (!dict[bc.parentDomainId]) dict[bc.parentDomainId] = [];
                dict[bc.parentDomainId].push(bc);
                return dict;
            },
            {}
        );
    const foundBoundedContextIds =
        new Set(Array
            .from(filteredContexts)
            .map(context => context.id)
        );

    const foundDomainIds =
        new Set(Array
            .from(filteredContexts)
            .map(context => context.parentDomainId)
        );


    // starting from the domains of the matched bounded contexts
    const relevantDomainsIds = findRelevantDomainIds(
        domainDictionary,
        highlightMode ? foundDomainIds : new Set(Object.keys(boundedContextsToDisplay))
    );

    function isRelevantDomain(domain) {
        return relevantDomainsIds.has(domain.id);
    }

    function shouldMapDomain(domain) {
        return highlightMode || isRelevantDomain(domain);
    }

    function mapDomain(domain) {
        // domain.boundedContexts is not filled in subdomains of the domain
        const boundedContexts = boundedContextsToDisplay[domain.id] || [];
        return {
            name: domain.name,
            wasFound: isRelevantDomain(domain),
            children: [
                ...domain.subdomains.filter(shouldMapDomain).map(mapDomain),
                ...boundedContexts.map(mapBoundedContext),
            ],
        };
    }

    function mapBoundedContext(boundedContext) {
        return {
            name: boundedContext.name,
            isBoundedContext: true,
            wasFound: foundBoundedContextIds.has(boundedContext.id),
            children: [],
        };
    }

    return {
        name: "Domain Landscape",
        children: domains
            .filter((domain) => !domain.parentDomainId)
            .filter(shouldMapDomain)
            .map(mapDomain),
    };
}