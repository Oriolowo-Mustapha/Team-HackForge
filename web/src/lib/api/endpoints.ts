import { z } from "zod";
import { api } from "./client";
import * as s from "./schemas";

/** Typed wrappers for every FarmHand API call used by the app. */

const qs = (params: Record<string, string | number | boolean | null | undefined>) => {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") search.set(key, String(value));
  }
  const str = search.toString();
  return str ? `?${str}` : "";
};

export const sessionApi = {
  get: () => api("/api/session", { schema: s.sessionSchema, quiet401: true }),
  login: (email: string, password: string) =>
    api<{ userId: string; email: string; role: string; fullName: string | null; mustChangePassword: boolean }>("/api/session/login", {
      body: { email, password },
      quiet401: true,
    }),
  register: (body: { email: string; password: string; fullName: string; phoneNumber?: string }) =>
    api<{ role: string }>("/api/session/register", { body, quiet401: true }),
  logout: (everywhere = false) => api("/api/session/logout", { body: { everywhere }, quiet401: true }),
};

export const authApi = {
  updateProfile: (body: { fullName: string | null; phoneNumber: string | null }) => api("auth/me", { method: "PUT", body, schema: s.sessionUserSchema }),
  changePassword: (currentPassword: string, newPassword: string) =>
    api<{ message: string }>("auth/change-password", { body: { currentPassword, newPassword } }),
  forgotPassword: (email: string) => api<{ message: string }>("auth/forgot-password", { body: { email }, quiet401: true }),
  resetPassword: (token: string, newPassword: string) => api<{ message: string }>("auth/reset-password", { body: { token, newPassword }, quiet401: true }),
};

export const systemApi = {
  info: () => api("system/info", { schema: s.systemInfoSchema, quiet401: true }),
};

export type TtsResponse = { audioUrl: string; provider: string; voice: string; creditsCharged: number };

export const voiceApi = {
  /** Synthesize text to speech via Spitch (with YarnGPT fallback). Returns a data-URI or URL of the audio. */
  speak: (text: string, options: { voice?: string; targetLanguage?: string } = {}) =>
    api<TtsResponse>("/api/voice/respond", { body: { text, voice: options.voice ?? null, targetLanguage: options.targetLanguage ?? null } }),
  /** List available TTS voices. */
  voices: () => api<{ id: string; name: string; language: string; gender: string; accent: string }[]>("/api/voice/voices"),
};

export type FarmLocationInput = {
  displayName: string;
  location: string;
  latitude?: number | null;
  longitude?: number | null;
  cityOrTown?: string | null;
  state?: string | null;
  country?: string | null;
  locationSource?: "device_gps" | "manual" | string | null;
};

export const farmsApi = {
  list: () => api("farms", { schema: z.array(s.farmSummarySchema) }),
  get: (farmId: string) => api(`farms/${farmId}`, { schema: s.farmSummarySchema }),
  create: (input: string | FarmLocationInput, location?: string) => {
    const body = typeof input === "string" ? { displayName: input, location: location ?? "" } : input;
    return api("farms", { body, schema: s.idResponseSchema });
  },
  update: (farmId: string, input: string | FarmLocationInput, location?: string) => {
    const body = typeof input === "string" ? { displayName: input, location: location ?? "" } : input;
    return api(`farms/${farmId}`, { method: "PUT", body, schema: s.farmSummarySchema });
  },
  overview: (farmId: string, signal?: AbortSignal) => api(`farms/${farmId}/overview`, { schema: s.dashboardSchema, signal }),
};

export type GroupInput = { name: string; description?: string | null; species?: string | null; headcount?: number | null };
export type AnimalInput = {
  categoryId: string;
  name: string;
  species: string;
  breed?: string | null;
  ageMonths?: number | null;
  sex?: string | null;
  notes?: string | null;
  diseaseHistory?: string | null;
  recentGestationDate?: string | null;
  lastVeterinaryVisit?: string | null;
  vaccinations?: string | null;
  lastLactationDate?: string | null;
  parentage?: string | null;
};

export const livestockApi = {
  groups: (farmId: string) => api(`livestock/categories/details${qs({ farmId })}`, { schema: z.array(s.groupSchema) }),
  createGroup: (farmId: string, input: GroupInput) => api("livestock/categories", { body: { farmId, ...input }, schema: s.idResponseSchema }),
  updateGroup: (groupId: string, input: GroupInput) => api(`livestock/categories/${groupId}`, { method: "PUT", body: input, schema: s.groupSchema }),
  animals: (farmId: string, filters: { categoryId?: string; q?: string; species?: string; includeInactive?: boolean } = {}) =>
    api(`livestock/animals${qs({ farmId, ...filters })}`, { schema: z.array(s.animalSchema) }),
  animal: (animalId: string) => api(`livestock/animals/${animalId}`, { schema: s.animalDetailSchema }),
  createAnimal: (farmId: string, input: AnimalInput) => api("livestock/animals", { body: { farmId, ...input }, schema: s.animalSchema }),
  updateAnimal: (animalId: string, input: AnimalInput) =>
    api(`livestock/animals/${animalId}`, { method: "PUT", body: { animalId, ...input }, schema: s.animalSchema }),
  archive: (animalId: string) => api(`livestock/animals/${animalId}/archive`, { method: "POST", schema: s.animalSchema }),
  restore: (animalId: string) => api(`livestock/animals/${animalId}/restore`, { method: "POST", schema: s.animalSchema }),
  uploadPhoto: (animalId: string, file: Blob) => {
    const form = new FormData();
    form.append("image", file, "photo.jpg");
    return api(`livestock/animals/${animalId}/photo`, { formData: form, schema: s.animalSchema });
  },
};

export type SubmitObservationInput = {
  farmId: string;
  livestockAnimalId?: string | null;
  livestockCategoryId?: string | null;
  text?: string;
  audio?: Blob | null;
  image?: Blob | null;
  idempotencyKey: string;
  manualEntry?: boolean;
  mortalityCount?: number | null;
  waterConsumption?: string | null;
  feedConsumption?: string | null;
  eggProduction?: string | null;
};

export const observationsApi = {
  list: (filters: { farmId?: string; status?: number; animalId?: string; categoryId?: string; page?: number; pageSize?: number }) =>
    api(`observations${qs(filters)}`, { schema: s.pagedSchema(s.observationSummarySchema) }),
  get: (id: string) => api(`observations/${id}`, { schema: s.observationDetailSchema }),
  submit: (input: SubmitObservationInput, options: { signal?: AbortSignal; onUploadProgress?: (fraction: number) => void } = {}) => {
    const form = new FormData();
    form.append("farmId", input.farmId);
    if (input.livestockAnimalId) form.append("livestockAnimalId", input.livestockAnimalId);
    if (input.livestockCategoryId) form.append("livestockCategoryId", input.livestockCategoryId);
    if (input.text) form.append("text", input.text);
    if (input.audio) form.append("audio", input.audio, audioFileName(input.audio.type));
    if (input.image) form.append("image", input.image, "photo.jpg");
    form.append("idempotencyKey", input.idempotencyKey);
    if (input.manualEntry) {
      form.append("manualEntry", "true");
      if (input.mortalityCount !== null && input.mortalityCount !== undefined) form.append("mortalityCount", String(input.mortalityCount));
      if (input.waterConsumption) form.append("waterConsumption", input.waterConsumption);
      if (input.feedConsumption) form.append("feedConsumption", input.feedConsumption);
      if (input.eggProduction) form.append("eggProduction", input.eggProduction);
    }
    return api("observations", { formData: form, schema: s.observationDetailSchema, idempotencyKey: input.idempotencyKey, ...options });
  },
  confirm: (
    id: string,
    body: {
      farmerNotes?: string | null;
      overrideSeverity?: number | null;
      overrideReason?: string | null;
      mortalityCount?: number | null;
      waterConsumption?: string | null;
      feedConsumption?: string | null;
      eggProduction?: string | null;
    },
  ) => api(`observations/${id}/confirm`, { body, schema: s.confirmResultSchema }),
  discard: (id: string) => api(`observations/${id}`, { method: "DELETE" }),
};

function audioFileName(type: string) {
  if (type.includes("ogg")) return "voice-note.ogg";
  if (type.includes("mp4") || type.includes("m4a") || type.includes("aac")) return "voice-note.m4a";
  if (type.includes("wav")) return "voice-note.wav";
  if (type.includes("mpeg")) return "voice-note.mp3";
  return "voice-note.webm";
}

export type IncidentFilters = {
  farmId?: string;
  status?: number;
  severity?: number;
  openOnly?: boolean;
  animalId?: string;
  categoryId?: string;
  from?: string;
  to?: string;
  q?: string;
  page?: number;
  pageSize?: number;
};

export const incidentsApi = {
  search: (filters: IncidentFilters) => api(`incidents${qs(filters)}`, { schema: s.pagedSchema(s.incidentListItemSchema) }),
  caseFile: (incidentId: string) => api(`incidents/${incidentId}/case`, { schema: s.incidentCaseSchema }),
  create: (body: {
    farmId: string;
    livestockAnimalId: string | null;
    livestockCategoryId: string | null;
    incidentType: string;
    severity: number;
    triggeringObservations: string;
    evidenceSummary: string;
  }) => api("incidents", { body, schema: s.idResponseSchema }),
  setStatus: (incidentId: string, newStatus: number, followUpDate?: string | null) =>
    api(`incidents/${incidentId}/status`, { body: { incidentId, newStatus, followUpDate: followUpDate ?? null } }),
  addAction: (incidentId: string, description: string, priority: string) =>
    api(`incidents/${incidentId}/actions`, { body: { incidentId, description, priority }, schema: s.idResponseSchema }),
  completeAction: (incidentId: string, actionId: string, notes?: string) =>
    api(`incidents/${incidentId}/actions/${actionId}/complete`, { body: { notes: notes ?? null }, schema: s.actionSchema }),
  addFollowUp: (incidentId: string, farmerResponse: number, notes: string | null) =>
    api("follow-ups", { body: { incidentId, farmerResponse, notes }, schema: s.idResponseSchema }),
  savePreventionPlan: (incidentId: string, content: string) =>
    api(`VetCoordination/incidents/${incidentId}/prevention-plan`, { body: { incidentId, content, generatedFromHistory: false }, schema: s.idResponseSchema }),
};

export const vetsApi = {
  directory: (q?: string) => api(`VetCoordination/vets${qs({ q })}`, { schema: s.vetDirectorySchema }),
  requests: (filters: { scope?: "open" | "assigned"; status?: number; incidentId?: string; page?: number; pageSize?: number } = {}) =>
    api(`VetCoordination/vet-requests${qs(filters)}`, { schema: s.pagedSchema(s.vetRequestSchema) }),
  request: (id: string) => api(`VetCoordination/vet-requests/${id}`, { schema: s.vetRequestSchema }),
  createRequest: (body: {
    incidentId: string;
    requestType: number;
    vetPartnerId: string | null;
    description: string | null;
    preferredVisitDate: string | null;
  }) => api<{ id: string }>(`VetCoordination/incidents/${body.incidentId}/vet-request`, { body }),
  accept: (id: string) => api(`VetCoordination/vet-requests/${id}/respond`, { body: { vetRequestId: id }, schema: s.vetRequestSchema }),
  decline: (id: string, reason: string | null) => api(`VetCoordination/vet-requests/${id}/decline`, { body: { reason }, schema: s.vetRequestSchema }),
  cancel: (id: string) => api(`VetCoordination/vet-requests/${id}/cancel`, { method: "POST", schema: s.vetRequestSchema }),
  complete: (id: string, notes: string | null) => api(`VetCoordination/vet-requests/${id}/complete`, { body: { notes }, schema: s.vetRequestSchema }),
  createTreatmentPlan: (body: { vetRequestId: string; instructionsText: string; language: string | null; schedule: { action: string; dueDate: string }[] }) =>
    api("treatment-plans", { body: { ...body, authoredByVetId: null }, schema: s.idResponseSchema }),
  completeTreatmentStep: (planId: string, stepId: string) => api(`treatment-plans/${planId}/steps/${stepId}/complete`, { method: "POST", schema: s.treatmentStepSchema }),
  payoutAccount: () => api("vet/me/payout-account", { schema: s.payoutAccountSchema }),
  updatePayoutAccount: (body: { bankCode: string; accountNumber: string; accountName: string; country: string }) =>
    api("vet/me/payout-account", { method: "PUT", body }),
};

export const paymentsApi = {
  quote: (vetRequestId: string) => api(`payments/quote${qs({ vetRequestId })}`, { schema: s.paymentQuoteSchema }),
  list: (filters: { vetRequestId?: string; status?: string; page?: number; pageSize?: number } = {}) =>
    api(`payments${qs(filters)}`, { schema: s.pagedSchema(s.paymentSchema) }),
  initiate: (vetRequestId: string, amount: number, currency: string, idempotencyKey: string) =>
    api("payments/initiate", { body: { vetRequestId, amount, currency }, idempotencyKey, schema: s.initiatePaymentSchema }),
  verify: (transactionReference: string) => api("payments/verify", { body: { transactionReference }, schema: s.paymentStatusSchema }),
  cancel: (reference: string) => api(`payments/${encodeURIComponent(reference)}/cancel`, { method: "POST", schema: s.paymentStatusSchema }),
};

export const adminApi = {
  overview: () => api("admin/overview", { schema: s.platformOverviewSchema }),
  users: (filters: { role?: string; q?: string; active?: boolean; page?: number; pageSize?: number }) =>
    api(`admin/users${qs(filters)}`, { schema: s.pagedSchema(s.adminUserSchema) }),
  setUserActive: (userId: string, active: boolean) => api(`admin/users/${userId}/${active ? "activate" : "deactivate"}`, { method: "POST" }),
  vets: (page = 1) => api(`admin/vets${qs({ page, pageSize: 100 })}`, { schema: s.pagedSchema(s.adminVetSchema) }),
  registerVet: (body: { email: string; temporaryPassword: string | null; fullName: string; specialization: string; contact: string; phoneNumber: string | null }) =>
    api<{ vetId: string; credentialsEmailed: boolean; message: string }>("admin/vets", { body }),
  resetVetCredentials: (vetId: string, temporaryPassword: string | null) =>
    api<{ credentialsEmailed: boolean; message: string }>(`admin/vets/${vetId}/reset-credentials`, { body: { temporaryPassword } }),
  deactivateVet: (vetId: string) => api(`admin/vets/${vetId}`, { method: "DELETE" }),
  activateVet: (vetId: string) => api(`admin/vets/${vetId}/activate`, { method: "POST" }),
  farms: (filters: { q?: string; page?: number }) => api(`admin/farms${qs({ ...filters, pageSize: 25 })}`, { schema: s.pagedSchema(s.adminFarmSchema) }),
  incidents: (filters: IncidentFilters) => api(`admin/incidents${qs(filters)}`, { schema: s.pagedSchema(s.incidentListItemSchema) }),
  payments: (filters: { status?: string; page?: number }) => api(`admin/payments${qs({ ...filters, pageSize: 25 })}`, { schema: s.pagedSchema(s.paymentSchema) }),
  audit: (filters: { eventType?: string; actorId?: string; page?: number }) =>
    api(`audit-events${qs({ ...filters, pageSize: 50 })}`, { schema: s.pagedSchema(s.auditEventSchema) }),
  system: () => api("admin/system", { schema: s.systemHealthSchema }),
};
