let lastId = 0;

/**
 * Create a unique id with the given prefix for the global application.
 *
 * @param prefix an optional prefix
 */
export function uniqueId(prefix = "contexture_id_") {
  lastId++;
  return `${prefix}${lastId}`;
}
