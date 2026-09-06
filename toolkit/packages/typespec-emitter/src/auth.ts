/**
 * Service authentication → IRAuth.
 *
 * Reads the service's authentication metadata via `@typespec/http`'s
 * `getAuthentication` (service-level; ARM injects a default Azure AD OAuth2
 * scheme through `@armProviderNamespace`, but any `@useAuth` lands in the same
 * state) and maps it onto the IR's `IRAuth` node.
 *
 * The mapping mirrors http's `Authentication` (OR of options, AND of schemes),
 * dropping the non-serializable `model` back-reference and — for oauth2 —
 * flattening flow scopes into the de-duplicated `scopes` a client credential
 * needs (flow URLs are not used by a service SDK).
 */

import type { Namespace } from "@typespec/compiler";
import { getAuthentication } from "@typespec/http";
import type { HttpAuth, OAuth2Flow } from "@typespec/http";
import type { IRAuth, IRAuthOption, IRAuthScheme, IROAuth2Flow } from "@azure-client-engine/ir";

import type { BuildContext } from "./context.js";

/** Resolve the service's authentication, or undefined if none is declared. */
export function authFrom(ctx: BuildContext, serviceNs: Namespace): IRAuth | undefined {
  const authentication = getAuthentication(ctx.program, serviceNs);
  if (authentication === undefined) return undefined;

  const options: IRAuthOption[] = [];
  for (const option of authentication.options) {
    const schemes = option.schemes
      .map(schemeFrom)
      .filter((s): s is IRAuthScheme => s !== undefined);
    if (schemes.length > 0) options.push({ schemes });
  }

  return options.length > 0 ? { options } : undefined;
}

/** Map one `@typespec/http` scheme onto an `IRAuthScheme`. */
function schemeFrom(auth: HttpAuth): IRAuthScheme | undefined {
  const desc = auth.description !== undefined ? { description: auth.description } : {};

  switch (auth.type) {
    case "oauth2": {
      const flows = auth.flows.map(flowFrom);
      return { kind: "oauth2", id: auth.id, ...desc, flows };
    }
    case "apiKey":
      return { kind: "apiKey", id: auth.id, ...desc, in: auth.in, name: auth.name };
    case "http":
      // Covers BasicAuth ("Basic") and BearerAuth ("Bearer"); both have type "http".
      return { kind: "http", id: auth.id, ...desc, scheme: auth.scheme };
    case "openIdConnect":
      return { kind: "openIdConnect", id: auth.id, ...desc, openIdConnectUrl: auth.openIdConnectUrl };
    case "noAuth":
      return { kind: "noAuth", id: auth.id };
    default:
      // Unknown scheme kind: skip rather than emit an invalid IR.
      return undefined;
  }
}

/** Map one `@typespec/http` OAuth2Flow onto an `IROAuth2Flow`, preserving every URL + scope. */
function flowFrom(flow: OAuth2Flow): IROAuth2Flow {
  const scopes = flow.scopes.map((s) => ({
    value: s.value,
    ...(s.description !== undefined ? { description: s.description } : {}),
  }));
  const refresh = flow.refreshUrl !== undefined ? { refreshUrl: flow.refreshUrl } : {};

  switch (flow.type) {
    case "authorizationCode":
      return {
        type: "authorizationCode",
        authorizationUrl: flow.authorizationUrl,
        tokenUrl: flow.tokenUrl,
        ...refresh,
        scopes,
      };
    case "implicit":
      return { type: "implicit", authorizationUrl: flow.authorizationUrl, ...refresh, scopes };
    case "password":
      return { type: "password", authorizationUrl: flow.authorizationUrl, ...refresh, scopes };
    case "clientCredentials":
      return { type: "clientCredentials", tokenUrl: flow.tokenUrl, ...refresh, scopes };
  }
}
