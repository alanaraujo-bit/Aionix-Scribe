import {
  pgTable,
  uuid,
  text,
  integer,
  boolean,
  timestamp,
  uniqueIndex,
  index,
} from "drizzle-orm/pg-core";

export const users = pgTable(
  "users",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    authProvider: text("auth_provider").notNull(),
    authProviderSubject: text("auth_provider_subject").notNull(),
    email: text("email").notNull(),
    stripeCustomerId: text("stripe_customer_id"),
    createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
    updatedAt: timestamp("updated_at", { withTimezone: true }).notNull().defaultNow(),
  },
  (table) => [
    uniqueIndex("users_auth_provider_subject_idx").on(table.authProvider, table.authProviderSubject),
  ],
);

export const subscriptions = pgTable("subscriptions", {
  id: uuid("id").primaryKey().defaultRandom(),
  userId: uuid("user_id")
    .notNull()
    .unique()
    .references(() => users.id),
  stripeSubscriptionId: text("stripe_subscription_id").notNull().unique(),
  tier: text("tier").notNull(),
  billingPeriod: text("billing_period").notNull(),
  status: text("status").notNull(),
  currentPeriodStart: timestamp("current_period_start", { withTimezone: true }).notNull(),
  currentPeriodEnd: timestamp("current_period_end", { withTimezone: true }).notNull(),
  cancelAtPeriodEnd: boolean("cancel_at_period_end").notNull().default(false),
  stripeUpdatedAt: timestamp("stripe_updated_at", { withTimezone: true }).notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
});

export const usagePeriods = pgTable(
  "usage_periods",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    userId: uuid("user_id")
      .notNull()
      .references(() => users.id),
    periodStart: timestamp("period_start", { withTimezone: true }).notNull(),
    periodEnd: timestamp("period_end", { withTimezone: true }).notNull(),
    tier: text("tier").notNull(),
    quotaSeconds: integer("quota_seconds"),
    consumedSeconds: integer("consumed_seconds").notNull().default(0),
    createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
  },
  (table) => [
    uniqueIndex("usage_periods_user_period_start_idx").on(table.userId, table.periodStart),
  ],
);

export const usageEvents = pgTable(
  "usage_events",
  {
    id: uuid("id").primaryKey().defaultRandom(),
    userId: uuid("user_id")
      .notNull()
      .references(() => users.id),
    recordingId: uuid("recording_id").notNull(),
    usagePeriodId: uuid("usage_period_id")
      .notNull()
      .references(() => usagePeriods.id),
    audioSeconds: integer("audio_seconds").notNull(),
    billedSeconds: integer("billed_seconds").notNull(),
    state: text("state", { enum: ["in_flight", "billed", "failed", "rejected"] }).notNull(),
    geminiFinishReason: text("gemini_finish_reason"),
    modelVersion: text("model_version"),
    promptTokens: integer("prompt_tokens"),
    candidateTokens: integer("candidate_tokens"),
    totalTokens: integer("total_tokens"),
    createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
    settledAt: timestamp("settled_at", { withTimezone: true }),
  },
  (table) => [
    uniqueIndex("usage_events_user_recording_idx").on(table.userId, table.recordingId),
    index("usage_events_user_created_at_idx").on(table.userId, table.createdAt.desc()),
  ],
);

export const webhookEvents = pgTable("webhook_events", {
  stripeEventId: text("stripe_event_id").primaryKey(),
  type: text("type").notNull(),
  receivedAt: timestamp("received_at", { withTimezone: true }).notNull().defaultNow(),
  processedAt: timestamp("processed_at", { withTimezone: true }),
});

/// Lista de espera das assinaturas, alimentada pelo site (P6) enquanto o checkout não existe.
/// Deliberadamente separada de `users`: quem entra aqui NÃO tem conta, não autenticou nada e pode
/// nunca virar usuário — misturar as duas coisas poluiria a tabela de identidade com endereços não
/// verificados. `email` é chave primária: reenviar o mesmo e-mail é idempotente, não duplica.
export const waitlistSignups = pgTable("waitlist_signups", {
  email: text("email").primaryKey(),
  // Plano de interesse quando a pessoa clica num card específico; nulo quando vem do rodapé.
  interestedTier: text("interested_tier", { enum: ["essencial", "premium", "ultra"] }),
  source: text("source"),
  createdAt: timestamp("created_at", { withTimezone: true }).notNull().defaultNow(),
});
