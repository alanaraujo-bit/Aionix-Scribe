CREATE TABLE "waitlist_signups" (
	"email" text PRIMARY KEY NOT NULL,
	"interested_tier" text,
	"source" text,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL
);
