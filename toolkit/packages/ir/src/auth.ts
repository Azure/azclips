/**
 * Service authentication.
 *
 * Mirrors `@typespec/http`'s `Authentication` shape (an OR of options, each an
 * AND of schemes) so a client generator can reconstruct the credential(s) a
 * service requires. Attached per-service on `IRSource.auth` (a sibling of
 * `apiVersion` / `server`), since auth is a service-level fact.
 *
 * Fidelity note: oauth2 carries the full flow set faithfully — every flow's
 * `type`, URLs (`authorizationUrl` / `tokenUrl` / `refreshUrl`), and scopes
 * (value + description). The only thing dropped is the non-serializable `model`
 * back-reference from `HttpAuthBase`. A generator that only needs scopes can
 * read them from `flows[].scopes`.
 */

import { z } from "zod";

/** A single oauth2 scope: its wire value and optional description. */
export type IROAuth2Scope = {
  value: string;
  description?: string | undefined;
};

export const IROAuth2ScopeSchema: z.ZodType<IROAuth2Scope> = z
  .object({
    value: z.string().min(1),
    description: z.string().optional(),
  })
  .strict();

/**
 * An oauth2 flow, discriminated by `type` — mirrors `@typespec/http`'s four
 * `OAuth2Flow` variants exactly (each carries only the URLs its grant uses).
 */
export type IROAuth2Flow =
  | {
      type: "authorizationCode";
      authorizationUrl: string;
      tokenUrl: string;
      refreshUrl?: string | undefined;
      scopes: IROAuth2Scope[];
    }
  | {
      type: "implicit";
      authorizationUrl: string;
      refreshUrl?: string | undefined;
      scopes: IROAuth2Scope[];
    }
  | {
      type: "password";
      authorizationUrl: string;
      refreshUrl?: string | undefined;
      scopes: IROAuth2Scope[];
    }
  | {
      type: "clientCredentials";
      tokenUrl: string;
      refreshUrl?: string | undefined;
      scopes: IROAuth2Scope[];
    };

export const IROAuth2FlowSchema: z.ZodType<IROAuth2Flow> = z.discriminatedUnion("type", [
  z
    .object({
      type: z.literal("authorizationCode"),
      authorizationUrl: z.string().min(1),
      tokenUrl: z.string().min(1),
      refreshUrl: z.string().min(1).optional(),
      scopes: z.array(IROAuth2ScopeSchema),
    })
    .strict(),
  z
    .object({
      type: z.literal("implicit"),
      authorizationUrl: z.string().min(1),
      refreshUrl: z.string().min(1).optional(),
      scopes: z.array(IROAuth2ScopeSchema),
    })
    .strict(),
  z
    .object({
      type: z.literal("password"),
      authorizationUrl: z.string().min(1),
      refreshUrl: z.string().min(1).optional(),
      scopes: z.array(IROAuth2ScopeSchema),
    })
    .strict(),
  z
    .object({
      type: z.literal("clientCredentials"),
      tokenUrl: z.string().min(1),
      refreshUrl: z.string().min(1).optional(),
      scopes: z.array(IROAuth2ScopeSchema),
    })
    .strict(),
]);

export type IRAuthScheme =
  | {
      kind: "oauth2";
      /** Scheme id (e.g. "azure_auth"). */
      id: string;
      description?: string | undefined;
      /** Full oauth2 flows (type, URLs, scopes) as declared. */
      flows: IROAuth2Flow[];
    }
  | {
      kind: "apiKey";
      id: string;
      description?: string | undefined;
      /** Where the key travels on the wire. */
      in: "header" | "query" | "cookie";
      /** Header/query/cookie name carrying the key. */
      name: string;
    }
  | {
      kind: "http";
      id: string;
      description?: string | undefined;
      /** HTTP authorization scheme. */
      scheme: "Basic" | "Bearer";
    }
  | {
      kind: "openIdConnect";
      id: string;
      description?: string | undefined;
      openIdConnectUrl: string;
    }
  | {
      /** API is explicitly unsecured (used to override auth off). */
      kind: "noAuth";
      id: string;
    };

export const IRAuthSchemeSchema: z.ZodType<IRAuthScheme> = z.discriminatedUnion("kind", [
  z
    .object({
      kind: z.literal("oauth2"),
      id: z.string().min(1),
      description: z.string().optional(),
      flows: z.array(IROAuth2FlowSchema),
    })
    .strict(),
  z
    .object({
      kind: z.literal("apiKey"),
      id: z.string().min(1),
      description: z.string().optional(),
      in: z.enum(["header", "query", "cookie"]),
      name: z.string().min(1),
    })
    .strict(),
  z
    .object({
      kind: z.literal("http"),
      id: z.string().min(1),
      description: z.string().optional(),
      scheme: z.enum(["Basic", "Bearer"]),
    })
    .strict(),
  z
    .object({
      kind: z.literal("openIdConnect"),
      id: z.string().min(1),
      description: z.string().optional(),
      openIdConnectUrl: z.string().min(1),
    })
    .strict(),
  z
    .object({
      kind: z.literal("noAuth"),
      id: z.string().min(1),
    })
    .strict(),
]);

/** All schemes in an option must be satisfied together (AND). */
export type IRAuthOption = {
  schemes: IRAuthScheme[];
};

export const IRAuthOptionSchema: z.ZodType<IRAuthOption> = z
  .object({
    schemes: z.array(IRAuthSchemeSchema).min(1),
  })
  .strict();

/** Any single option can authenticate the client (OR). */
export type IRAuth = {
  options: IRAuthOption[];
};

export const IRAuthSchema: z.ZodType<IRAuth> = z
  .object({
    options: z.array(IRAuthOptionSchema).min(1),
  })
  .strict();
