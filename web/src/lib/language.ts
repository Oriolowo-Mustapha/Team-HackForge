/** Canonical report-language codes used by the server (matches the Spitch/YarnGPT speech engines). */
const names: Record<string, string> = {
  yo: "Yoruba",
  ha: "Hausa",
  ig: "Igbo",
  pcm: "Nigerian Pidgin",
  en: "English",
};

/** Human-readable name for a language code ("yo" -> "Yoruba"), or null when unknown. */
export function languageName(code: string | null | undefined): string | null {
  return code ? names[code.toLowerCase()] ?? null : null;
}
