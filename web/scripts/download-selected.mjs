// Downloads the visually-picked Commons files at 2000px into the source dir and
// writes meta.json for the optimizer. Usage: node scripts/download-selected.mjs <source-dir>
import { writeFile, mkdir } from "node:fs/promises";
import path from "node:path";

const source = process.argv[2];
if (!source) {
  console.error("Usage: node scripts/download-selected.mjs <source-dir>");
  process.exit(1);
}
await mkdir(source, { recursive: true });

// name -> exact Commons file title (picked from the reviewed contact sheets)
const picks = {
  "farmer-goats": "File:Mali goat roundup 2008.jpeg",
  "vet-cattle": "File:Vet Students Cattle RVC 2006-08.jpg",
  "poultry-worker": "File:Baby chicken vaccination.jpg",
  "hens-closeup": "File:Barnevelder Hen Head.jpg",
  "layer-house": "File:Baby chickens in a grower broiler house. (25018838441).jpg",
  "hen-free-range": "File:Free range chicken flock.jpg",
  "hen-garden": "File:Chickens in the garden (spring 2020) 3.jpg",
  "chicks": "File:Fot. Andrew Skowron (32206265387).jpg",
  "goats-pen": "File:A herd of goats and sheep in the shed at Mlinza Farm 05.jpg",
  "goat-portrait": "File:Brown-goat-award.jpg",
  "goat-walking": "File:Gorge du Verdon Goat 0254.jpg",
  "goat-black": "File:Black Goat BN3Q4171.jpg",
  "sheep": "File:Sheep eating grass Eastern Cape.jpg",
  "cow": "File:Frygian cow and zebu - Kisoro, Uganda.jpg",
  "cow-care": "File:IMG Dairy cow at Sgola-April 2024.jpg",
};

const API = "https://commons.wikimedia.org/w/api.php";
const HEADERS = { "user-agent": "FarmHand-image-fetch/1.0 (contact: dev@farmhand.example)", accept: "application/json" };
const strip = (h) => (h ?? "").replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function info(title) {
  const params = new URLSearchParams({
    action: "query", format: "json", formatversion: "2",
    titles: title,
    prop: "imageinfo",
    iiprop: "url|size|mime|extmetadata",
    iiurlwidth: "2000",
  });
  const res = await fetch(`${API}?${params}`, { headers: HEADERS });
  if (!res.ok) throw new Error(`info ${res.status}`);
  const data = await res.json();
  const page = (data?.query?.pages ?? [])[0];
  const i = page?.imageinfo?.[0];
  if (!i) throw new Error("no imageinfo");
  return {
    thumbUrl: i.thumburl,
    pageUrl: i.descriptionurl,
    width: i.width,
    height: i.height,
    license: strip(i.extmetadata?.LicenseShortName?.value ?? ""),
    creator: strip(i.extmetadata?.Artist?.value ?? "") || "Unknown author",
    desc: strip(i.extmetadata?.ImageDescription?.value ?? "").slice(0, 150),
  };
}

async function download(url, file) {
  const res = await fetch(url, { headers: HEADERS });
  if (!res.ok) throw new Error(`download ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  if (buf.length < 50_000) throw new Error(`suspiciously small download (${buf.length} bytes)`);
  await writeFile(file, buf);
}

const meta = {};
const failed = [];
for (const [name, title] of Object.entries(picks)) {
  try {
    const i = await info(title);
    await sleep(700);
    await download(i.thumbUrl, path.join(source, `${name}.jpg`));
    meta[name] = {
      name,
      title: title.replace(/^File:/, "").replace(/\.(jpe?g|jpeg|png|tiff?|webp)$/i, "").trim(),
      creator: i.creator,
      license: i.license || "Unknown",
      source: "Wikimedia Commons",
      foreign_landing_url: i.pageUrl,
      description: i.desc,
      originalSize: `${i.width}x${i.height}`,
    };
    console.log("fetched", name, "←", title, `[${i.license}]`, `${i.width}x${i.height}`);
  } catch (e) {
    failed.push(name);
    console.error("FAILED", name, String(e));
  }
  await sleep(400);
}

await writeFile(path.join(source, "meta.json"), JSON.stringify(meta, null, 2) + "\n");
if (failed.length) {
  console.error("MISSING:", failed.join(", "));
  process.exitCode = 2;
}
