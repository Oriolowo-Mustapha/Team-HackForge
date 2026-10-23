const LOCALE = "en-NG";

export function formatMoney(amount: number, currency = "NGN"): string {
  return new Intl.NumberFormat(LOCALE, { style: "currency", currency, maximumFractionDigits: amount % 1 === 0 ? 0 : 2 }).format(amount);
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat(LOCALE).format(value);
}

export function formatDate(value: string | Date | null | undefined, options: Intl.DateTimeFormatOptions = { day: "numeric", month: "short", year: "numeric" }): string {
  if (!value) return "—";
  const date = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat(LOCALE, options).format(date);
}

export function formatDateTime(value: string | Date | null | undefined): string {
  return formatDate(value, { day: "numeric", month: "short", hour: "numeric", minute: "2-digit" });
}

const RTF = new Intl.RelativeTimeFormat(LOCALE, { numeric: "auto" });

/** "2 hours ago", "tomorrow", "in 3 days". */
export function formatRelative(value: string | Date | null | undefined, now = Date.now()): string {
  if (!value) return "—";
  const date = typeof value === "string" ? new Date(value) : value;
  const diff = date.getTime() - now;
  const abs = Math.abs(diff);
  const minute = 60_000, hour = 60 * minute, day = 24 * hour;
  if (abs < minute) return "just now";
  if (abs < hour) return RTF.format(Math.round(diff / minute), "minute");
  if (abs < day) return RTF.format(Math.round(diff / hour), "hour");
  if (abs < 14 * day) return RTF.format(Math.round(diff / day), "day");
  return formatDate(date);
}

export function greeting(date = new Date()): string {
  const hour = date.getHours();
  if (hour < 12) return "Good morning";
  if (hour < 17) return "Good afternoon";
  return "Good evening";
}

/** "Funmilayo Adeyemi" -> "Funmilayo"; "Dr. Chinedu Okafor" -> "Dr. Okafor". */
export function firstName(fullName: string | null | undefined, fallback = "there"): string {
  const parts = (fullName ?? "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return fallback;
  if (/^dr\.?$/i.test(parts[0]!)) return `Dr. ${parts[parts.length - 1]}`;
  return parts[0]!;
}

export function initials(name: string | null | undefined): string {
  const parts = (name ?? "").trim().split(/\s+/).filter((p) => !/^dr\.?$/i.test(p));
  return (parts.slice(0, 2).map((p) => p[0]?.toUpperCase()).join("") || "FH").slice(0, 2);
}

export function pluralize(count: number, singular: string, plural = `${singular}s`): string {
  return `${formatNumber(count)} ${count === 1 ? singular : plural}`;
}

export function formatAge(months: number | null | undefined): string | null {
  if (months === null || months === undefined) return null;
  if (months < 12) return `${months} mo`;
  const years = Math.floor(months / 12);
  const rest = months % 12;
  return rest ? `${years} yr ${rest} mo` : `${years} yr`;
}
