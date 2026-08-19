CREATE TABLE "gemini_calls" (
	"id" uuid PRIMARY KEY DEFAULT gen_random_uuid() NOT NULL,
	"model_version" text NOT NULL,
	"audio_bytes" integer,
	"prompt_tokens" integer NOT NULL,
	"candidate_tokens" integer NOT NULL,
	"total_tokens" integer NOT NULL,
	"cost_usd" numeric(12, 6) NOT NULL,
	"finish_reason" text,
	"empty_result" boolean DEFAULT false NOT NULL,
	"gemini_latency_ms" integer,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE INDEX "gemini_calls_created_at_idx" ON "gemini_calls" USING btree ("created_at" DESC NULLS LAST);