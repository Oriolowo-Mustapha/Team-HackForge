/** Editorial photography (CC0, see /images/credits.json) chosen by species for headers and fallbacks. */
const bySpecies: Record<string, string[]> = {
  poultry: ["/images/hens-closeup", "/images/hen-free-range", "/images/layer-house", "/images/hen-garden"],
  goat: ["/images/goat-portrait", "/images/goats-pen", "/images/goat-walking", "/images/goat-black"],
  sheep: ["/images/sheep"],
  cattle: ["/images/cow", "/images/cow-care"],
  default: ["/images/farmer-goats"],
};

export function speciesKey(species: string | null | undefined): keyof typeof bySpecies {
  const s = (species ?? "").toLowerCase();
  if (/(chick|hen|poultry|layer|broiler|bird|fowl|duck|turkey)/.test(s)) return "poultry";
  if (/goat/.test(s)) return "goat";
  if (/(sheep|ewe|ram|lamb)/.test(s)) return "sheep";
  if (/(cow|cattle|bull|calf|heifer)/.test(s)) return "cattle";
  return "default";
}

function hash(value: string) {
  let h = 0;
  for (let i = 0; i < value.length; i++) h = (h * 31 + value.charCodeAt(i)) | 0;
  return Math.abs(h);
}

/** Returns a base path (without extension); use `${base}.webp` or `${base}-<w>.webp` variants. */
export function speciesPhoto(species: string | null | undefined, seed = ""): string {
  const list = bySpecies[speciesKey(species)] ?? bySpecies.default!;
  return list[hash(seed) % list.length]!;
}

/** Image URLs from the API: local uploads are served through the BFF; Cloudinary URLs are used directly. */
export function mediaUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  if (url.startsWith("/api/media/")) return `/api/bff/media/${url.slice("/api/media/".length)}`;
  return url;
}

const smallWidths: Record<string, number> = {
  "/images/farmer-goats": 800,
  "/images/vet-cattle": 800,
  "/images/poultry-worker": 800,
  "/images/hens-closeup": 800,
  "/images/layer-house": 600,
  "/images/hen-free-range": 600,
  "/images/hen-garden": 600,
  "/images/chicks": 600,
};

/** The smaller rendition of a bundled photo (cards, thumbnails); the large one is `${base}.webp`. */
export function smallPhoto(base: string): string {
  return `${base}-${smallWidths[base] ?? 480}.webp`;
}
