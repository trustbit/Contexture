interface Array<T> {
  sortAlphabeticallyBy(selectProperty: (arg0: T) => string): Array<T>;
}

Array.prototype.sortAlphabeticallyBy = function (selectProperty) {
  return this.toSorted((a, b) => selectProperty(a).toLowerCase().localeCompare(selectProperty(b)));
};
