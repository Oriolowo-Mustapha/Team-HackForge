// Fetches high-resolution, freely licensed farm/livestock photos from Wikimedia Commons
// into a source directory, ready for `node scripts/optimize-images.mjs <source-dir>`.
// Usage: node scripts/fetch-commons-images.mjs <source-dir>
//
// Each entry is searched on Commons, filtered by resolution/aspect/license, downloaded as a
// 2000px-wide JPEG thumb (plenty above the 1600px hero rendition), and recorded in meta.json
// so the optimizer can write public/images/credits.json with correct attribution.
import { writeFile, mkdir, stat } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";

const source = process.argv[2];
if (!source) {
  console.error("Usage: node scripts/fetch-commons-images.mjs <source-dir>");
  process.exit(1);
}
await mkdir(source, { recursive: true });

// name -> Commons search query (joined with 'filemime:jpeg -svg' to stay on photographs)
const wanted = {
  "farmer-goats": "goats Nigeria village",
  "vet-cattle": "cattle vaccination Africa",
  "poultry-worker": "poultry farmer working",
  "hens-closeup": "hen chicken closeup",
  "layer-house": "poultry house interior chicken",
  "hen-free-range": "free range hen",
  "hen-garden": "hen grass",
  "chicks": "chicks brooder poultry",
  "goats-pen": "goats enclosure",
  "goat-portrait": "goat portrait head",
  "goat-walking": "goat standing field",
  "goat-black": "black goat",
  "sheep": "sheep flock Africa",
  "cow": "cow zebu Africa",
  "cow-care": "cow close-up nose",
};

const API = "https://commons.wikimedia.org/w/api.php";
const HEADERS = {
  "user-agent": "FarmHand-image-fetch/1.0 (contact: dev@farmhand.ai)",
  accept: "application/json",
};

const licensesOk = (name) =>
  /^(public domain|cc0|cc by([-\sd.0-9])*$|cc by-sa)/i.test(name.trim()) && !/nc|nd/i.test(name);

function stripTags(html) {
  return (html ?? "")
    .replace(/<[^>]*>/g, "")
    .replace(/&#0?39;|&apos;|&#x27;/g, "'")
    .replace(/&quot;|&#0?34;/g, '"')
    .replace(/&amp;/g, "&")
    .replace(/\s+/g, " ")
    .trim();
}

/** Queries Commons for candidate files and returns normalized candidates. */
async function search(query, ct) {
  const params = new URLSearchParams({
    action: "query",
    format: "json",
    formatversion: "2",
    generator: "search",
    gsrsearch: `${query} filemime:jpeg`,
    gsrnamespace: "6",
    gsrlimit: "30",
    prop: "imageinfo",
    iiprop: "url|size|mime|extmetadata",
    iiurlwidth: "2000",
  });
  const res = await fetch(`${API}?${params}`, { headers: HEADERS, signal: ct });
  if (!res.ok) throw new Error(`Commons search failed: ${res.status}`);
  const data = await res.json();
  const pages = (data?.query?.pages ?? []).filter((p) => p?.imageinfo?.length);
  return pages.map((p) => {
    const info = p.imageinfo[0];
    const meta = info.extmetadata ?? {};
    return {
      title: p.title,
      pageUrl: info.descriptionurl,
      thumbUrl: info.thumburl,
      width: info.width,
      height: info.height,
      license: stripTags(meta.LicenseShortName?.value ?? ""),
      creator: stripTags(meta.Artist?.value ?? "") || "Unknown author",
      desc: stripTags(meta.ImageDescription?.value ?? "").slice(0, 120),
    };
  });
}

/** Picks the best candidate for a subject: big, landscape, friendly license. */
function pick(candidates, { minHero = 1600 }) {
  const passing = (c) =>
    c.width >= minHero + 200 &&
    c.height >= 500 &&
    c.width / c.height >= 1.15 &&
    c.width / c.height <= 2.6 &&
    licensesOk(c.license);
  return candidates.find(passing) ?? candidates.find((c) => c.width >= minHero + 200) ?? null;
}

async function download(url, file, ct) {
  const res = await fetch(url, { headers: HEADERS, signal: ct });
  if (!res.ok) throw new Error(`download ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  if (buf.length < 20_000) throw new Error(`suspiciously small download (${buf.length} bytes)`);
  await writeFile(file, buf);
}

const ct = new AbortController();
setTimeout(() => ct.abort(), 1000 * 60 * 8).unref();

const metaRows = [];
const missing = [];
for (const [name, query] of Object.entries(wanted)) {
  const target = path.join(source, `${name}.jpg`);
  if (existsSync(target)) {
    const s = await stat(target);
    if (s.size > 50_000) {
      console.log("kept existing", name);
      continue;
    }
  }
  let chosen = null;
  try {
    chosen = pick(await search(query, ct.signal), {});
  } catch (e) {
    console.error(`search failed for ${name}:`, String(e));
  }

  if (!chosen) {
    missing.push(name);
    console.error("NO CANDIDATE:", name, `(${query})`);
    continue;
  }

  try {
    await download(chosen.thumbUrl, target, ct.signal);
    metaRows.push({
      name,
      title: chosen.title.replace(/^File:/, "").replace(/\.(jpe?g|png|tiff?|webp)$/i, ""),
      creator: chosen.creator,
      license: chosen.license || "Unknown",
      source: "Wikimedia Commons",
      foreign_landing_url: chosen.pageUrl,
      originalSize: `${chosen.width}x${chosen.height}`,
    });
    console.log(
      "fetched", name, "←", chosen.title, chosen.license, chosen.width + "x" + chosen.height,
    );
  } catch (e) {
    missing.push(name);
    console.error(`download failed for ${name}:`, String(e));
  }
}

await writeFile(path.join(source, "meta.json"), JSON.stringify(Object.fromEntries(metaRows.map((m) => [m.name, m])), null, 2) + "\n");

if (missing.length) {
  console.error("MISSING:", missing.join(", "));
  process.exitCode = 2;
}
