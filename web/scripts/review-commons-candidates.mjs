// Builds labeled contact sheets (top N candidates per subject) so the right Commons photo
// can be chosen visually. Usage: node scripts/review-commons-candidates.mjs <out-dir> [subject1=idx ...]
import { writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";

const outDir = process.argv[2] ?? "sheets";
await mkdir(outDir, { recursive: true });

const queries = {
  "farmer-goats": "goats herder Africa village",
  "vet-cattle": "veterinarian cattle",
  "poultry-worker": "feeding chickens",
  "hens-closeup": "hen closeup",
  "layer-house": "chicken coop interior",
  "hen-free-range": "free range hen chicken",
  "hen-garden": "chickens garden",
  "chicks": "yellow chicks poultry",
  "goats-pen": "goats pen",
  "goat-portrait": "goat head portrait",
  "goat-walking": "domestic goat field",
  "goat-black": "black goat",
  "sheep": "sheep Africa farm",
  "cow": "zebu cow",
  "cow-care": "dairy cow milking",
};

const API = "https://commons.wikimedia.org/w/api.php";
const HEADERS = { "user-agent": "FarmHand-image-review/1.0", accept: "application/json" };
const thumbsWidth = 280;

// Optional: node review-commons-candidates.mjs <out-dir> --only=cow,cow-care
const onlyArg = process.argv.find((a) => a.startsWith("--only="));
const only = onlyArg ? new Set(onlyArg.slice(7).split(",")) : null;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function search(query, limit = 8) {
  const params = new URLSearchParams({
    action: "query", format: "json", formatversion: "2",
    generator: "search",
    gsrsearch: `${query} filemime:jpeg`,
    gsrnamespace: "6", gsrlimit: String(limit),
    prop: "imageinfo",
    iiprop: "url|size|mime|extmetadata",
    iiurlwidth: String(thumbsWidth),
  });
  const res = await fetch(`${API}?${params}`, { headers: HEADERS });
  if (!res.ok) throw new Error(`search ${res.status}`);
  const data = await res.json();
  return (data?.query?.pages ?? [])
    .filter((p) => p?.imageinfo?.length)
    .map((p) => ({
      title: p.title,
      thumbUrl: p.imageinfo[0].thumburl,
      pageUrl: p.imageinfo[0].descriptionurl,
      width: p.imageinfo[0].width,
      height: p.imageinfo[0].height,
      license: (p.imageinfo[0].extmetadata?.LicenseShortName?.value ?? "").replace(/<[^>]*>/g, ""),
      creator: (p.imageinfo[0].extmetadata?.Artist?.value ?? "unknown").replace(/<[^>]*>/g, " ").trim().slice(0, 40),
    }));
}

function label(text) {
  const svg = Buffer.from(
    `<svg width="${thumbsWidth}" height="28" xmlns="http://www.w3.org/2000/svg">
      <rect width="100%" height="100%" fill="black" opacity="0.65"/>
      <text x="8" y="20" font-family="sans-serif" font-size="16" font-weight="bold" fill="white">${text.replace(/[<>&]/g, "")}</text>
    </svg>`,
  );
  return sharp(svg);
}

for (const [name, query] of Object.entries(queries)) {
  if (only && !only.has(name)) continue;
  let candidates;
  try {
    candidates = (await search(query, 16)).filter((c) => c.width >= 1500 && c.height >= 600 && c.width / c.height <= 3.4 && c.width / c.height >= 0.55);
  } catch (e) {
    console.error(name, "search failed:", String(e));
    continue;
  }
  await sleep(1500); // stay under the Commons API rate limit
  if (candidates.length === 0) {
    console.error(name, "no candidates");
    continue;
  }

  // 4-across grid, each cell thumb + index label overlaid
  const cols = 4;
  const rows = Math.ceil(candidates.length / cols);
  const cellH = 200;
  const composite = [];
  const chosen = [];

  for (let i = 0; i < candidates.length; i++) {
    const c = candidates[i];
    try {
      const res = await fetch(c.thumbUrl, { headers: HEADERS });
      if (!res.ok) continue;
      const buf = Buffer.from(await res.arrayBuffer());
      await sharp(buf).resize(thumbsWidth, cellH - 28, { fit: "cover" }).webp().toFile(path.join(outDir, `${name}-${i}.jpg`));
      const placed = await sharp(path.join(outDir, `${name}-${i}.jpg`))
        .composite([{ input: await (await label(`#${i} ${c.license.slice(0, 20)}`)).png().toBuffer(), top: cellH - 28, left: 0 }])
        .jpeg().toBuffer();
      await sharp(placed).jpeg({ quality: 80 }).toFile(path.join(outDir, `${name}-${i}-labeled.jpg`));
      const row = Math.floor(i / cols), col = i % cols;
      composite.push({
        input: placed,
        top: row * cellH,
        left: col * thumbsWidth,
      });
      chosen.push(c);
    } catch (e) {
      console.error(name, i, "thumb failed:", String(e));
    }
  }

  const sheet = await sharp({
    create: { width: cols * thumbsWidth, height: rows * cellH, channels: 3, background: { r: 24, g: 24, b: 24 } },
  }).composite(composite).jpeg({ quality: 78 }).toBuffer();

  await sharp(sheet)
    .jpeg({ quality: 80 })
    .toFile(path.join(outDir, `sheet-${name}.jpg`));

  // Randomly print the list so numbers map to titles (order = candidates order used above).
  await writeFile(
    path.join(outDir, `mapping-${name}.json`),
    JSON.stringify(chosen.map((c, i) => ({ i, title: c.title, license: c.license, size: `${c.width}x${c.height}`, creator: c.creators ?? c.creator, url: c.pageUrl })), null, 2) + "\n",
  );
  console.log("sheet:", name, chosen.length, "candidates");
}
