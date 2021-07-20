
function findRelevantDomainIds(
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

export async function fetchData(baseApi, query) {
    const contexts = await fetchBoundedContexts(baseApi, query);
    const domains = await fetchDomains(baseApi);

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
            name: boundedContext.name,
            isBoundedContext: true,
            children: [],
        };
    }

    return {
        name: "root",
        children: domains
            .filter((domain) => !domain.parentDomainId)
            .filter(isRelevantDomain)
            .map(mapDomain),
    };
}