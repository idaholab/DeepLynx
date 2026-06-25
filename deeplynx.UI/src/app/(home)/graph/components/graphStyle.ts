const COLOR_BLIND_SAFE = [
  "#0072B2",
  "#E69F00",
  "#009E73",
  "#CC79A7",
  "#56B4E9",
  "#D55E00",
  "#332288",
  "#88CCEE",
  "#44AA99",
  "#117733",
  "#999933",
  "#CC6677",
  "#882255",
  "#AA4499",
];

const clampChannel = (value: number) => Math.max(0, Math.min(255, value));

const adjustHexColor = (hexColor: string, factor: number) => {
  const normalized = hexColor.replace("#", "");
  const red = parseInt(normalized.slice(0, 2), 16);
  const green = parseInt(normalized.slice(2, 4), 16);
  const blue = parseInt(normalized.slice(4, 6), 16);

  const toHex = (value: number) =>
    clampChannel(Math.round(value))
      .toString(16)
      .padStart(2, "0");

  return `#${toHex(red * factor)}${toHex(green * factor)}${toHex(blue * factor)}`;
};

export const getSizeForDepth = (depth: number) => {
  const maxSize = 24;
  const step = 3;
  const minSize = 10;
  return Math.max(maxSize - depth * step, minSize);
};

export const getUniqueClasses = (
  nodes: Array<{ classId?: number | null; className?: string | null }>
) =>
  Array.from(
    new Map<string, { key: string; label: string }>(
      nodes.map((node): [string, { key: string; label: string }] => {
        const key = node.classId != null ? String(node.classId) : "No Class";
        const label = node.className && node.className.trim() !== "" ? node.className : "No Class";
        console.log("className: ", node.className);
        return [key, { key, label }];
      }),
    ).values(),
  ).sort((left, right) => left.label.localeCompare(right.label));

export const buildClassColorMap = (
  nodes: Array<{ classId?: number | null; className?: string | null }>,
) => {

  const classEntries = getUniqueClasses(nodes);

  const map = new Map<string, string>();

  classEntries.forEach((entry, index) => {
    const baseColor = COLOR_BLIND_SAFE[index % COLOR_BLIND_SAFE.length];
    const cycle = Math.floor(index / COLOR_BLIND_SAFE.length);
    const variantFactor = cycle === 0 ? 1 : Math.max(0.72, 1 - cycle * 0.12);

    map.set(entry.key, adjustHexColor(baseColor, variantFactor));
  });

  return map;
};