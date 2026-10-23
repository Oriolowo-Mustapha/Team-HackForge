import { z } from "zod";

/**
 * Response schemas mirroring the FarmHand API DTOs (camelCase JSON, integer enums, ISO dates).
 * Responses are validated at the boundary so a contract drift fails loudly instead of rendering
 * misleading data.
 */

const id = z.guid();
const date = z.string();
const nullableString = z.string().nullable().optional().transform((v) => v ?? null);

export const pagedSchema = <T extends z.ZodTypeAny>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number(),
    pageSize: z.number(),
    totalCount: z.number(),
    totalPages: z.number(),
  });

export const sessionUserSchema = z.object({
  userId: id,
  email: z.string(),
  role: z.enum(["Farmer", "Vet", "Admin"]),
  fullName: nullableString,
  phoneNumber: nullableString,
  mustChangePassword: z.boolean(),
  createdAt: date,
  lastLoginAt: date.nullable().optional(),
  farms: z.array(z.object({ id, displayName: z.string(), location: z.string() })),
  vetProfile: z
    .object({
      id,
      name: z.string(),
      specialization: z.string(),
      contact: z.string(),
      isActive: z.boolean(),
      hasPayoutAccount: z.boolean(),
    })
    .nullable(),
});
export type SessionUser = z.infer<typeof sessionUserSchema>;

export const sessionSchema = z.union([
  z.object({ authenticated: z.literal(true), user: sessionUserSchema }),
  z.object({ authenticated: z.literal(false), expired: z.boolean().optional() }),
]);
export type Session = z.infer<typeof sessionSchema>;

export const systemInfoSchema = z.object({
  aiMode: z.string(),
  aiTextConfigured: z.boolean(),
  aiVisionConfigured: z.boolean(),
  transcriptionConfigured: z.boolean(),
  ruleBasedDemoFallbackEnabled: z.boolean(),
  paymentsMode: z.string(),
  emailEnabled: z.boolean(),
  imageStorage: z.string(),
  demoMode: z.boolean(),
});
export type SystemInfo = z.infer<typeof systemInfoSchema>;

export const farmSummarySchema = z.object({
  id,
  displayName: z.string(),
  location: z.string(),
  createdAt: date,
  categoryCount: z.number(),
  animalCount: z.number(),
  flockHeadcount: z.number(),
  openIncidentCount: z.number(),
  pendingObservationCount: z.number(),
});
export type FarmSummary = z.infer<typeof farmSummarySchema>;

export const incidentSchema = z.object({
  id,
  farmId: id,
  livestockAnimalId: id.nullable(),
  livestockCategoryId: id.nullable(),
  detectedAt: date,
  incidentType: z.string(),
  severity: z.number(),
  triggeringObservations: z.string(),
  evidenceSummary: z.string(),
  status: z.number(),
  ruleVersion: nullableString,
  followUpDate: date.nullable(),
  resolvedAt: date.nullable(),
});
export type Incident = z.infer<typeof incidentSchema>;

export const incidentListItemSchema = z.object({
  incident: incidentSchema,
  farmName: z.string(),
  subjectName: nullableString,
  subjectSpecies: nullableString,
  isFlockLevel: z.boolean(),
  vetRequestStatus: z.number().nullable(),
  vetName: nullableString,
});
export type IncidentListItem = z.infer<typeof incidentListItemSchema>;

export const observationSummarySchema = z.object({
  id,
  farmId: id,
  livestockAnimalId: id.nullable(),
  livestockCategoryId: id.nullable(),
  subjectName: nullableString,
  recordedAt: date,
  mortalityCount: z.number(),
  farmerNotes: nullableString,
  imageUrl: nullableString,
  confirmationStatus: z.number(),
  analysisSource: nullableString,
  inputChannels: nullableString,
  assessedSeverity: z.number().nullable(),
  incidentId: id.nullable(),
});
export type ObservationSummary = z.infer<typeof observationSummarySchema>;

export const assessmentSchema = z.object({
  severity: z.number(),
  summary: z.string(),
  recommendedAction: z.string(),
  requiresVetConsultation: z.boolean(),
  source: nullableString,
  isAi: z.boolean(),
  assessedAt: date,
});
export type Assessment = z.infer<typeof assessmentSchema>;

export const visionSchema = z.object({
  description: z.string(),
  detectedIssues: z.array(z.string()),
  confidence: z.number().nullable(),
  source: nullableString,
  available: z.boolean(),
});

export const observationDetailSchema = z.object({
  id,
  farmId: id,
  livestockAnimalId: id.nullable(),
  livestockCategoryId: id.nullable(),
  subjectName: nullableString,
  subjectSpecies: nullableString,
  recordedAt: date,
  mortalityCount: z.number(),
  waterConsumption: nullableString,
  feedConsumption: nullableString,
  eggProduction: nullableString,
  farmerNotes: nullableString,
  rawTranscript: nullableString,
  imageUrl: nullableString,
  vision: visionSchema.nullable(),
  confirmationStatus: z.number(),
  analysisSource: nullableString,
  analysisIsAi: z.boolean(),
  symptoms: z.array(z.string()),
  missingInformation: z.array(z.string()),
  inputChannels: nullableString,
  assessment: assessmentSchema.nullable(),
  incidentId: id.nullable(),
  reused: z.boolean(),
});
export type ObservationDetail = z.infer<typeof observationDetailSchema>;

export const confirmResultSchema = z.object({
  observationId: id,
  confirmationStatus: z.string(),
  aiSeverity: nullableString,
  finalSeverity: nullableString,
  severityOverridden: z.boolean(),
  aiDiagnosisSummary: nullableString,
  aiRecommendedAction: nullableString,
  aiRequiresVetConsultation: z.boolean(),
  incidentAutoCreated: z.boolean(),
  autoCreatedIncidentId: id.nullable(),
  assessmentSource: nullableString,
  assessmentIsAi: z.boolean(),
});
export type ConfirmResult = z.infer<typeof confirmResultSchema>;

export const vetSummarySchema = z.object({
  id,
  name: z.string(),
  specialization: z.string(),
  contact: z.string(),
  hasPayoutAccount: z.boolean(),
});
export type VetSummary = z.infer<typeof vetSummarySchema>;

export const participantSchema = z.object({ id, name: z.string(), email: nullableString, phone: nullableString });

export const paymentSchema = z.object({
  id,
  vetRequestId: id,
  status: z.string(),
  mode: z.string(),
  amount: z.number(),
  platformCommission: z.number(),
  totalAmount: z.number(),
  vetPayoutAmount: z.number().nullable(),
  currency: z.string(),
  transactionReference: z.string(),
  checkoutUrl: nullableString,
  vetSubaccountUsed: z.boolean(),
  failureReason: nullableString,
  createdAt: date,
  paidAt: date.nullable(),
  farmName: nullableString,
  incidentId: id.nullable(),
});
export type Payment = z.infer<typeof paymentSchema>;

export const vetRequestSchema = z.object({
  id,
  incidentId: id,
  requestType: z.number(),
  status: z.number(),
  farmerCost: z.number(),
  currency: z.string(),
  description: nullableString,
  preferredVisitDate: date.nullable(),
  createdAt: date,
  respondedAt: date.nullable(),
  completedAt: date.nullable(),
  completionNotes: nullableString,
  cancelledAt: date.nullable(),
  declinedAt: date.nullable(),
  declineReason: nullableString,
  vet: vetSummarySchema.nullable(),
  isTargeted: z.boolean(),
  incidentType: z.string(),
  incidentSeverity: z.number(),
  incidentStatus: z.number(),
  subjectName: nullableString,
  subjectSpecies: nullableString,
  farmName: z.string(),
  farmLocation: z.string(),
  farmer: participantSchema.nullable(),
  paymentStatus: z.string(),
  latestPayment: paymentSchema.nullable(),
  availableActions: z.array(z.string()),
});
export type VetRequest = z.infer<typeof vetRequestSchema>;

export const treatmentStepSchema = z.object({ id, action: z.string(), dueDate: date, isCompleted: z.boolean(), completedAt: date.nullable() });

export const treatmentPlanSchema = z.object({
  id,
  vetRequestId: id,
  authoredByVetId: z.string(),
  authorName: nullableString,
  instructionsText: z.string(),
  language: nullableString,
  createdAt: date,
  schedule: z.array(treatmentStepSchema),
});
export type TreatmentPlan = z.infer<typeof treatmentPlanSchema>;

export const followUpSchema = z.object({ id, incidentId: id, farmerResponse: z.number(), notes: nullableString, recordedAt: date });
export const actionSchema = z.object({
  id,
  incidentId: id,
  description: z.string(),
  priority: z.string(),
  status: z.string(),
  completedAt: date.nullable(),
  completionNotes: nullableString,
});
export type IncidentAction = z.infer<typeof actionSchema>;

export const timelineEntrySchema = z.object({ at: date, kind: z.string(), title: z.string(), detail: nullableString, actor: nullableString });

export const incidentCaseSchema = z.object({
  incident: incidentSchema,
  farmName: z.string(),
  farmLocation: z.string(),
  subjectName: nullableString,
  subjectSpecies: nullableString,
  isFlockLevel: z.boolean(),
  farmer: participantSchema.nullable(),
  sourceObservation: observationDetailSchema.nullable(),
  vetRequests: z.array(vetRequestSchema),
  treatmentPlans: z.array(treatmentPlanSchema),
  preventionPlan: z.object({ id, incidentId: id, content: z.string(), generatedFromHistory: z.boolean() }).nullable(),
  followUps: z.array(followUpSchema),
  actions: z.array(actionSchema),
  timeline: z.array(timelineEntrySchema),
  allowedTransitions: z.array(z.number()),
  access: z.string(),
  canRequestVet: z.boolean(),
});
export type IncidentCase = z.infer<typeof incidentCaseSchema>;

export const groupSchema = z.object({
  id,
  farmId: id,
  name: z.string(),
  description: nullableString,
  species: nullableString,
  headcount: z.number(),
  registeredAnimals: z.number(),
  createdAt: date,
});
export type Group = z.infer<typeof groupSchema>;

export const animalSchema = z.object({
  id,
  farmId: id,
  categoryId: id,
  categoryName: nullableString,
  name: z.string(),
  species: z.string(),
  breed: nullableString,
  ageMonths: z.number().nullable(),
  sex: nullableString,
  notes: nullableString,
  photoUrl: nullableString,
  isActive: z.boolean(),
  createdAt: date,
  diseaseHistory: nullableString,
  recentGestationDate: date.nullable(),
  lastVeterinaryVisit: date.nullable(),
  vaccinations: nullableString,
  lastLactationDate: date.nullable(),
  parentage: nullableString,
  openIncidentCount: z.number(),
  highestOpenSeverity: z.number().nullable(),
  lastObservedAt: date.nullable(),
});
export type Animal = z.infer<typeof animalSchema>;

export const animalDetailSchema = z.object({
  animal: animalSchema,
  observations: z.array(observationSummarySchema),
  incidents: z.array(incidentSchema),
});

export const dashboardSchema = z.object({
  farm: z.object({ id, ownerId: z.string(), displayName: z.string(), location: z.string(), createdAt: date }),
  counts: z.object({
    animals: z.number(),
    flockHeadcount: z.number(),
    groups: z.number(),
    openIncidents: z.number(),
    urgentIncidents: z.number(),
    pendingObservations: z.number(),
    activeVetRequests: z.number(),
    observationsLast7Days: z.number(),
    resolvedLast30Days: z.number(),
  }),
  attentionIncidents: z.array(incidentSchema),
  pendingObservations: z.array(observationSummarySchema),
  activeVetRequests: z.array(vetRequestSchema),
  reminders: z.array(z.object({ incidentId: id, title: z.string(), dueAt: date, kind: z.string(), isOverdue: z.boolean() })),
  recentActivity: z.array(
    z.object({ at: date, kind: z.string(), title: z.string(), detail: nullableString, relatedId: id.nullable(), severity: z.number().nullable() }),
  ),
  openBySeverity: z.array(z.object({ severity: z.number(), count: z.number() })),
  last14Days: z.array(z.object({ date: z.string(), observations: z.number(), deaths: z.number() })),
  nextAction: z.string(),
  nextActionKind: z.string(),
});
export type Dashboard = z.infer<typeof dashboardSchema>;

export const paymentQuoteSchema = z.object({
  vetRequestId: id,
  vetFee: z.number(),
  commissionPercentage: z.number(),
  platformCommission: z.number(),
  total: z.number(),
  currency: z.string(),
  mode: z.string(),
  vetName: nullableString,
  vetPayoutSplit: z.boolean(),
  canPay: z.boolean(),
  reason: nullableString,
  pendingPayment: paymentSchema.nullable(),
});
export type PaymentQuote = z.infer<typeof paymentQuoteSchema>;

export const initiatePaymentSchema = z.object({
  paymentId: id,
  status: z.string(),
  mode: z.string(),
  transactionReference: z.string(),
  checkoutUrl: nullableString,
  platformCommission: z.number(),
  totalAmount: z.number(),
  vetPayoutAmount: z.number(),
  vetSubaccountUsed: z.boolean(),
  idempotencyKey: z.string(),
});

export const paymentStatusSchema = z.object({ payment: paymentSchema, message: z.string() });
export type PaymentStatus = z.infer<typeof paymentStatusSchema>;

export const vetDirectorySchema = z.object({
  items: z.array(z.object({ id, name: z.string(), specialization: z.string(), contact: z.string(), hasPayoutAccount: z.boolean() })),
});

export const payoutAccountSchema = z.object({
  configured: z.boolean(),
  bankCode: nullableString,
  accountName: nullableString,
  accountNumberMasked: nullableString,
  country: nullableString,
  subaccountConfigured: z.boolean(),
});

const countByKey = z.object({ key: z.string(), count: z.number() });
export const auditEventSchema = z.object({
  id,
  actorId: z.string(),
  eventType: z.string(),
  relatedRecordId: id.nullable(),
  timestamp: date,
  details: nullableString,
});
export type AuditEvent = z.infer<typeof auditEventSchema>;

export const platformOverviewSchema = z.object({
  counts: z.object({
    usersByRole: z.array(countByKey),
    activeUsers: z.number(),
    farms: z.number(),
    animals: z.number(),
    flockHeadcount: z.number(),
    observationsByStatus: z.array(countByKey),
    incidentsByStatus: z.array(countByKey),
    incidentsBySeverity: z.array(countByKey),
    vetRequestsByStatus: z.array(countByKey),
    paymentsByStatus: z.array(countByKey),
    completedLivePaymentsTotal: z.number(),
    completedSimulatedPaymentsTotal: z.number(),
    platformCommissionTotal: z.number(),
  }),
  recentEvents: z.array(auditEventSchema),
  integrations: systemInfoSchema,
});
export type PlatformOverview = z.infer<typeof platformOverviewSchema>;

export const adminUserSchema = z.object({
  id,
  email: z.string(),
  fullName: nullableString,
  phoneNumber: nullableString,
  role: z.string(),
  isActive: z.boolean(),
  mustChangePassword: z.boolean(),
  createdAt: date,
  lastLoginAt: date.nullable(),
  vetPartnerId: id.nullable(),
});
export type AdminUser = z.infer<typeof adminUserSchema>;

export const adminFarmSchema = z.object({
  id,
  displayName: z.string(),
  location: z.string(),
  ownerEmail: z.string(),
  ownerName: nullableString,
  createdAt: date,
});

export const adminVetSchema = z.object({
  id,
  userId: id.nullable(),
  name: z.string(),
  email: z.string(),
  specialization: z.string(),
  contact: z.string(),
  isActive: z.boolean(),
  hasPayoutAccount: z.boolean(),
  createdAt: date,
});
export type AdminVet = z.infer<typeof adminVetSchema>;

export const systemHealthSchema = z.object({
  database: z.object({ connected: z.boolean(), appliedMigrations: z.number(), pendingMigrations: z.array(z.string()) }),
  integrations: systemInfoSchema,
  serverTimeUtc: date,
  version: nullableString,
});

export const idResponseSchema = z.object({ id });
