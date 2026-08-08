import { Pool } from "pg";
import { drizzle } from "drizzle-orm/node-postgres";
import * as schema from "./schema.js";

// Sem checagem de presença no import: mantém o servidor de pé mesmo sem DATABASE_URL
// configurada (dev local sem banco), igual ao padrão já usado em gemini.ts para GEMINI_API_KEY.
// Uma URL ausente só se manifesta como falha de conexão quando algo de fato consulta o banco.
export const pool = new Pool({ connectionString: process.env.DATABASE_URL });
export const db = drizzle(pool, { schema });
