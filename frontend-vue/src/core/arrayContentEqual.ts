export function arrayContentEqual(a: any[], b: any[]): boolean {
  if (a.length !== b.length) {
    return false;
  }

  const sortedA = a.slice().sort();
  const sortedB = b.slice().sort();

  for (let i = 0; i < sortedA.length; i++) {
    if (JSON.stringify(sortedA[i]) !== JSON.stringify(sortedB[i])) {
      return false;
    }
  }

  return true;
}
