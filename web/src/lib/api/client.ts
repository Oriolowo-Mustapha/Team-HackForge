import type { z } from "zod";

/** A normalised API failure (RFC 7807 problem details from the API or the BFF). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  readonly detail: string | null;
  readonly fieldErrors: Record<string, string[]>;
  readonly traceId: string | null;

  constructor(status: number, title: string, options: { code?: string | null; detail?: string | null; fieldErrors?: Record<string, string[]>; traceId?: string | null } = {}) {
    super(options.detail ?? title);
    this.name = "ApiError";
    this.status = status;
    this.code = options.code ?? null;
    this.detail = options.detail ?? null;
    this.fieldErrors = options.fieldErrors ?? {};
    this.traceId = options.traceId ?? null;
  }

  /** The best user-facing sentence for this error. */
  get userMessage(): string {
    if (this.status === 0) return "You appear to be offline. Your work has not been lost — try again when you're connected.";
    const first = Object.values(this.fieldErrors)[0]?.[0];
    return this.detail ?? first ?? this.message;
  }

  get isNetwork() {
    return this.status === 0;
  }
}

export type RequestOptions<T> = {
  method?: "GET" | "POST" | "PUT" | "DELETE" | "PATCH";
  body?: unknown;
  formData?: FormData;
  schema?: z.ZodType<T>;
  signal?: AbortSignal;
  idempotencyKey?: string;
  /** Do not trigger the global "session expired" handler on 401 (used by the session probe). */
  quiet401?: boolean;
  /** Reports real upload progress (0–1) for multipart bodies; uses XMLHttpRequest under the hood. */
  onUploadProgress?: (fraction: number) => void;
};

/** fetch-compatible request that reports upload progress. Resolves to a standard Response. */
function xhrRequest(url: string, init: { method: string; headers: Record<string, string>; body?: BodyInit; signal?: AbortSignal }, onProgress: (fraction: number) => void): Promise<Response> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(init.method, url);
    xhr.withCredentials = true;
    for (const [name, value] of Object.entries(init.headers)) xhr.setRequestHeader(name, value);
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress(event.loaded / event.total);
    };
    xhr.upload.onload = () => onProgress(1);
    xhr.onload = () => {
      const headers = new Headers();
      for (const line of xhr.getAllResponseHeaders().trim().split(/[\r\n]+/)) {
        const index = line.indexOf(":");
        if (index > 0) headers.append(line.slice(0, index).trim(), line.slice(index + 1).trim());
      }
      resolve(new Response(xhr.status === 204 ? null : xhr.responseText, { status: xhr.status, headers }));
    };
    xhr.onerror = () => reject(new TypeError("Network request failed"));
    xhr.onabort = () => reject(new DOMException("Aborted", "AbortError"));
    init.signal?.addEventListener("abort", () => xhr.abort());
    xhr.send(init.body as XMLHttpRequestBodyInit | null | undefined);
  });
}

type ExpiredListener = () => void;
const expiredListeners = new Set<ExpiredListener>();

export function onSessionExpired(listener: ExpiredListener) {
  expiredListeners.add(listener);
  return () => {
    expiredListeners.delete(listener);
  };
}

export function notifySessionExpired() {
  expiredListeners.forEach((listener) => listener());
}

async function parseError(response: Response): Promise<ApiError> {
  let body: Record<string, unknown> = {};
  try {
    const text = await response.text();
    body = text ? (JSON.parse(text) as Record<string, unknown>) : {};
  } catch {
    body = {};
  }

  const title = typeof body.title === "string" ? body.title : typeof body.error === "string" ? body.error : defaultTitle(response.status);
  const detail = typeof body.detail === "string" ? body.detail : typeof body.error === "string" ? body.error : null;
  const errors = body.errors && typeof body.errors === "object" ? (body.errors as Record<string, string[]>) : {};
  return new ApiError(response.status, title, {
    code: typeof body.code === "string" ? body.code : null,
    detail,
    fieldErrors: errors,
    traceId: typeof body.traceId === "string" ? body.traceId : null,
  });
}

function defaultTitle(status: number) {
  if (status === 401) return "Please sign in again.";
  if (status === 403) return "You don't have permission to do that.";
  if (status === 404) return "We couldn't find that.";
  if (status === 409) return "That change conflicts with the latest data. Refresh and try again.";
  if (status === 413) return "That file is too large.";
  if (status === 429) return "Too many requests. Please wait a moment.";
  if (status >= 500) return "Something went wrong on our side. Please try again.";
  return "The request could not be completed.";
}

/**
 * Calls the FarmHand API through the same-origin BFF (`/api/bff/*`). Never call the API directly
 * from components: this function handles credentials, CSRF headers, problem details, schema
 * validation, cancellation and session expiry in one place.
 */
export async function api<T = unknown>(path: string, options: RequestOptions<T> = {}): Promise<T> {
  const method = options.method ?? (options.body !== undefined || options.formData ? "POST" : "GET");
  const headers: Record<string, string> = { accept: "application/json" };
  if (method !== "GET") headers["x-farmhand-csrf"] = "1";
  if (options.idempotencyKey) headers["idempotency-key"] = options.idempotencyKey;

  let body: BodyInit | undefined;
  if (options.formData) body = options.formData;
  else if (options.body !== undefined) {
    headers["content-type"] = "application/json";
    body = JSON.stringify(options.body);
  }

  const url = path.startsWith("/api/") ? path : `/api/bff/${path.replace(/^\/+/, "")}`;

  let response: Response;
  try {
    response = options.onUploadProgress && body
      ? await xhrRequest(url, { method, headers, body, signal: options.signal }, options.onUploadProgress)
      : await fetch(url, { method, headers, body, signal: options.signal, credentials: "same-origin", cache: "no-store" });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new ApiError(0, "Network error", { code: "network" });
  }

  if (!response.ok) {
    const error = await parseError(response);
    if (response.status === 401 && !options.quiet401) notifySessionExpired();
    throw error;
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  const json: unknown = text ? JSON.parse(text) : undefined;

  if (!options.schema) return json as T;
  const parsed = options.schema.safeParse(json);
  if (!parsed.success) {
    console.error("Unexpected API response for", path, parsed.error.issues.slice(0, 5));
    throw new ApiError(500, "The server returned data in an unexpected format.", { code: "contract" });
  }
  return parsed.data;
}

export function newIdempotencyKey(prefix = "fh"): string {
  const random = typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID().replace(/-/g, "") : Math.random().toString(36).slice(2);
  return `${prefix}-${random}`;
}
