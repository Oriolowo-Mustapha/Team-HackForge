// Converts the source photographs (see public/images/credits.json) into responsive WebP files.
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

// name -> goal: focal crop gravity, then [hero width, card width]. Every source must be
// at least the hero width so no rendition is ever upscaled.
const photos = {
  "farmer-goats": ["attention", [1600, 800]],
  "vet-cattle": ["attention", [1600, 800]],
  "poultry-worker": ["attention", [1600, 800]],
  "hens-closeup": ["attention", [1600, 800]],
  "layer-house": ["attention", [1600, 800]],
  "hen-free-range": ["attention", [1600, 800]],
  "hen-garden": ["attention", [1600, 800]],
  "chicks": ["attention", [1600, 800]],
  "goats-pen": ["attention", [1600, 800]],
  "goat-portrait": ["attention", [1600, 800]],
  "goat-walking": ["attention", [1600, 800]],
  "goat-black": ["attention", [1600, 800]],
  "sheep": ["attention", [1600, 800]],
  "cow": ["attention", [1600, 800]],
  "cow-care": ["attention", [1600, 800]],
};

const heroQuality = 85;
const cardQuality = 82;

const meta = JSON.parse(await readFile(path.join(source, "meta.json"), "utf8"));
const credits = [];

for (const [name, [, widths]] of Object.entries(photos)) {
  const input = path.join(source, `${name}.jpg`);
  for (const width of widths) {
    const suffix = width === widths[0] ? "" : `-${width}`;
    const quality = width === widths[0] ? heroQuality : cardQuality;
    await sharp(input)
      .rotate()
      .resize({ width, withoutEnlargement: true })
      .webp({ quality, effort: 6 })
      .toFile(path.join(outDir, `${name}${suffix}.webp`));
  }
  const m = meta[name];
  credits.push({
    file: `/images/${name}.webp`,
    title: m?.title ?? name,
    creator: m?.creator ?? null,
    license: m?.license ?? "CC BY",
    source: m?.source ?? null,
    url: m?.foreign_landing_url ?? null,
  });
  console.log("optimized", name);
}

await writeFile(path.join(outDir, "credits.json"), JSON.stringify(credits, null, 2) + "\n");
