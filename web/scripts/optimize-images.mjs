// Converts the source photographs (CC0, see public/images/credits.json) into responsive WebP files.
// Usage: node scripts/optimize-images.mjs <source-dir>
import sharp from "sharp";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";

const source = process.argv[2];
if (!source) {
  console.error("Usage: node scripts/optimize-images.mjs <source-dir>");
  process.exit(1);
}

const outDir = path.resolve("public/images");
await mkdir(outDir, { recursive: true });

// name -> [focal crop gravity, widths]
const photos = {
  "farmer-goats": ["attention", [1600, 800]],
  "vet-cattle": ["attention", [1600, 800]],
  "poultry-worker": ["attention", [1600, 800]],
  "hens-closeup": ["attention", [1600, 800]],
  "layer-house": ["centre", [1200, 600]],
  "hen-free-range": ["attention", [1200, 600]],
  "hen-garden": ["attention", [1200, 600]],
  "chicks": ["centre", [1200, 600]],
  "goats-pen": ["attention", [960, 480]],
  "goat-portrait": ["attention", [960, 480]],
  "goat-walking": ["attention", [960, 480]],
  "goat-black": ["attention", [960, 480]],
  "sheep": ["attention", [960, 480]],
  "cow": ["attention", [960, 480]],
  "cow-care": ["attention", [960, 480]],
};

const meta = JSON.parse(await readFile(path.join(source, "meta.json"), "utf8"));
const credits = [];

for (const [name, [, widths]] of Object.entries(photos)) {
  const input = path.join(source, `${name}.jpg`);
  for (const width of widths) {
    const suffix = width === widths[0] ? "" : `-${width}`;
    await sharp(input)
      .rotate()
      .resize({ width, withoutEnlargement: true })
      .webp({ quality: width >= 1200 ? 72 : 70, effort: 6 })
      .toFile(path.join(outDir, `${name}${suffix}.webp`));
  }
  const m = meta[name];
  credits.push({
    file: `/images/${name}.webp`,
    title: m?.title ?? name,
    creator: m?.creator ?? null,
    license: (m?.license ?? "cc0").toUpperCase(),
    source: m?.source ?? null,
    url: m?.foreign_landing_url ?? null,
  });
  console.log("optimized", name);
}

await writeFile(path.join(outDir, "credits.json"), JSON.stringify(credits, null, 2) + "\n");
