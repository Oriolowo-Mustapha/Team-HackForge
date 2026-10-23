// Renders the FarmHand mark into PWA, Apple and favicon PNGs.
import sharp from "sharp";
import { readFile, mkdir } from "node:fs/promises";

const mark = await readFile("public/brand/mark.svg");
await mkdir("public/icons", { recursive: true });

// "any" icons keep the rounded square; maskable icons get a full-bleed background with the
// mark inside the 80% safe zone so launchers can crop them to any shape.
const maskable = Buffer.from(
  (await readFile("public/brand/mark.svg", "utf8"))
    .replace('<rect width="512" height="512" rx="120" fill="#0B0B06"/>', '<rect width="512" height="512" fill="#0B0B06"/><g transform="translate(51.2 51.2) scale(0.8)">')
    .replace("</svg>", "</g></svg>"),
);

const jobs = [
  [mark, 192, "icon-192.png"],
  [mark, 512, "icon-512.png"],
  [maskable, 192, "maskable-192.png"],
  [maskable, 512, "maskable-512.png"],
  [maskable, 180, "apple-touch-icon.png"],
  [mark, 32, "favicon-32.png"],
  [mark, 16, "favicon-16.png"],
];

for (const [svg, size, name] of jobs) {
  await sharp(svg, { density: 384 }).resize(size, size).png({ compressionLevel: 9 }).toFile(`public/icons/${name}`);
  console.log("icon", name);
}
