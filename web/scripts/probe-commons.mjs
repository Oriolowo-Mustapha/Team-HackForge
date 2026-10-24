// Prints raw Commons search results for a query so we can see why filters reject them.
import { writeFile } from "node:fs/promises";
import path from "node:path";

const API = "https://commons.wikimedia.org/w/api.php";
const RUN_ID = Date.now().toString(36).slice(-9);
const HEADERS = {
  "user-agent": `FarmHand-image-review/1.0 (${RUN_ID}; contact: dev@farmhand.example)`,
  accept: "application/json",
};

const strip = (h) => (h ?? "").replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();

for (const q of process.argv.slice(2)) {
  const params = new URLSearchParams({
    action: "query", format: "json", formatversion: "2",
    generator: "search",
    gsrsearch: `${q} filemime:jpeg`,
    gsrnamespace: "6", gsrlimit: "12",
    prop: "imageinfo",
    iiprop: "url|size|mime|extmetadata",
  });
  const res = await fetch(`${API}?${params}`, { headers: HEADERS });
  const data = await res.json();
  const pages = data?.query?.pages ?? [];
  console.log(`\n=== ${q} -> ${res.status}, ${pages.length} results`);
  for (const p of pages) {
    const i = p.imageinfo?.[0];
    if (!i) continue;
    const lic = strip(i.extmetadata?.LicenseShortName?.value ?? "");
    console.log(`  ${i.width}x${i.height}  [${lic}]  ${p.title}`);
  }
  await writeFile(path.join("C:\\Users\\MUSTAPHA\\AppData\\Local\\Temp\\opencode", "run.txt"), RUN_ID + "\n");
}
