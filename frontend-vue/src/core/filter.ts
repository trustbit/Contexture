/**
 * Filter a given object by searching for a specific query in its properties.
 *
 * @param obj The object to search
 * @param query The string to search for
 * @param key An optional property key to limit the search to
 * @returns Returns a boolean indicating if the query was found in any of the object's properties.
 */
export function filter(obj: any, query: string, key?: string | undefined): boolean {
  for (const prop in obj) {
    if (key && prop !== key) {
      continue;
    }
    const propValue = obj[prop];
    if (!propValue) return false;
    if (Array.isArray(propValue)) {
      if (propValue.includes(query)) {
        return true;
      }
      const nestedResults = filterInArray(propValue as any[], query);
      if (nestedResults.length) {
        return true;
      }
    }
    else if (typeof propValue === "object") {
      return filter(propValue, query);
    } else if (typeof propValue === "string") {
      if (propValue.toLowerCase().includes(query?.toLowerCase())) {
        return true;
      }
    }
  }
  return false;
}

const filterInArray = (array: any[], query: string, key?: string): any[] => {
  return array.filter((o) => filter(o, query, key));
};
