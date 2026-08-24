// Azclips TypeSpec emitter.
//
// Consumes SdkContext from @azure-tools/typespec-client-generator-core (TCGC)
// and writes azclips-ir.json — the language-neutral IR that the C# codegen
// (tools/AzclipsCodegenTsp) renders into src/Extensions/<Rp>/ + src/PS/<Rp>/.
//
// Phase A scope: package boots, TypeSpec compiler successfully loads the emitter,
// $onEmit runs, produces a minimal azclips-ir.json plus a raw-sdk-dump.json we
// can inspect to shape the walker in Phase A.tcgc-walk.

import type { EmitContext } from "@typespec/compiler";
import { getDoc, getSummary } from "@typespec/compiler";
import { createSdkContext } from "@azure-tools/typespec-client-generator-core";
import type {
    Operation,
    Param,
    PathSlot,
    Resource,
    RpModel,
    ModelClass,
    ModelProp,
} from "./ir-types.js";
import { writeFile, mkdir } from "node:fs/promises";
import { join } from "node:path";

export interface AzclipsEmitterOptions {
    /**
     * Optional override for the RP canonical name. If unset, we derive it from
     * the service title / package namespace.
     */
    "rp-name"?: string;
    /**
     * Repo-relative path to the TypeSpec spec being compiled, e.g.
     * "specification/storage/Storage.Management". Recorded verbatim into
     * RpModel.SourceSpec so generated file headers can cite the exact spec
     * source. Passed by TspCompile.ps1 via `--option
     * @azclips/typespec-emitter.source-spec=<path>`. Falls back to the
     * TypeSpec compiler's resolved project root when omitted.
     */
    "source-spec"?: string;
    /**
     * Exact git commit SHA of the azure-rest-api-specs checkout that was
     * compiled to produce this IR. SourceSpec alone only names a directory —
     * its contents change over time — so pairing it with SourceCommit is what
     * makes a generated file's provenance actually reproducible/verifiable.
     * Passed by TspCompile.ps1 via `--option
     * @azclips/typespec-emitter.source-commit=<sha>` (computed via `git
     * rev-parse HEAD` in the specs checkout immediately before compiling, and
     * left unset if that checkout has uncommitted local changes, since the
     * commit SHA alone would then be misleading). No fallback: omitted means
     * unknown/unverifiable, not "assume HEAD".
     */
    "source-commit"?: string;
}

const PACKAGE_ID = "@azclips/typespec-emitter";

export async function $onEmit(context: EmitContext<AzclipsEmitterOptions>): Promise<void> {
    if (context.program.compilerOptions.noEmit) return;

    const sdkContext = await createSdkContext(context, PACKAGE_ID);
    // Give walker helpers access to the tsp Program so they can call getDoc().
    (globalThis as any).__azclips_program = context.program;

    // Phase A: derive a minimal RpModel so consumers can start wiring.
    const pkg = sdkContext.sdkPackage;
    const rootClient = pkg.clients[0];

    const rpName = context.options["rp-name"] ?? deriveRpName(rootClient);

    const resources = collectResources(rootClient);
    const models = pkg.models.map(mapModel);
    const providerNs = detectProviderNamespace(resources);
    const apiVersion = (rootClient as any)?.apiVersions?.slice(-1)[0] ?? "";

    // Provenance: prefer the explicit --option, fall back to the compiler's
    // resolved project root so azclips-ir.json always records *something*
    // concrete pointing back at the TypeSpec source that produced it.
    const sourceSpec = context.options["source-spec"] ?? context.program.projectRoot;
    // Unlike sourceSpec, sourceCommit has no fallback: if TspCompile.ps1 didn't
    // pass one (e.g. the specs checkout had uncommitted local changes), we
    // record "" rather than guessing, so consumers can tell "unknown" apart
    // from a real SHA.
    const sourceCommit = context.options["source-commit"] ?? "";

    const ir: RpModel = {
        Name: rpName,
        CliVerb: rpName.toLowerCase(),
        PsPrefix: rpName,
        ProviderNs: providerNs,
        ApiVersion: apiVersion,
        Description: (rootClient as any)?.description ?? "",
        SourceKind: "TypeSpec",
        SourceSpec: sourceSpec,
        SourceCommit: sourceCommit,
        Resources: resources,
        Models: models,
        Config: {
            emitterVersion: "0.1.0",
            phase: "A-tcgc-walk",
            operationCount: resources.reduce((n, r) => n + r.Operations.length, 0),
            resourceCount: resources.length,
            modelCount: models.length,
        },
    };

    const outDir = context.emitterOutputDir;
    await mkdir(outDir, { recursive: true });

    const irPath = join(outDir, "azclips-ir.json");
    await writeFile(irPath, JSON.stringify(ir, null, 2), "utf-8");
    /* eslint-disable no-console */
    console.log(`[azclips-emitter] wrote ${irPath}`);

    // Debug dump is best-effort — TCGC's SdkContext has circular refs on many
    // node types, so we defensively catch and skip on failure.
    try {
        const debugPath = join(outDir, "sdk-debug.json");
        const debug = {
            clients: pkg.clients.map((c) => summarizeClient(c as any, 0)),
            modelSample: pkg.models.slice(0, 5).map((m) => ({
                name: m.name,
                propCount: (m as any).properties?.length ?? 0,
            })),
            modelTotal: pkg.models.length,
        };
        await writeFile(debugPath, JSON.stringify(debug, null, 2), "utf-8");
        console.log(`[azclips-emitter] wrote ${debugPath}`);
    } catch (e) {
        console.log(`[azclips-emitter] skipping sdk-debug.json: ${(e as Error).message}`);
    }
    // TEMP diag for polymorphic union
    try {
        const diagPath = join(outDir, "poly-diag.json");
        const modelsToProbe = pkg.models.filter((m: any) => 
            m.name && (m.name === 'WorkflowProperties' || m.name.includes('WorkflowProfile'))
        ).slice(0, 8);
        const dump = modelsToProbe.map((m: any) => {
            const propSample = (m.properties ?? []).slice(0, 15).map((p: any) => ({ 
                name: p.name, optional: p.optional, 
                typeKind: p.type?.kind, typeName: p.type?.name,
                discriminatorValue: p.discriminatorValue,
            }));
            return {
                name: m.name, kind: m.__raw?.kind, propCount: m.properties?.length,
                baseModel: m.baseModel?.name, discriminator: m.discriminator,
                discriminatedSubtypes: m.discriminatedSubtypes ? Object.keys(m.discriminatedSubtypes) : null,
                propSample,
            };
        });
        await writeFile(diagPath, JSON.stringify(dump, null, 2), "utf-8");
        console.log(`[azclips-emitter] wrote ${diagPath}`);
    } catch (e) {
        console.log(`[azclips-emitter] skipping poly-diag: ${(e as Error).message}`);
    }
    // /diag-poly
}

    // Recursively walk a client + its operation groups so we can see where methods
    // really live. TCGC nests them under `.methods` and `.children`/`.subClients`
    // depending on version — check both.
    function summarizeClient(c: any, depth: number, seen: Set<any> = new Set()): any {
        if (seen.has(c)) return { name: c?.name, note: "already-visited" };
        seen.add(c);
        const groups = c.subClients ?? c.children ?? c.operationGroups ?? [];
        return {
            depth,
            name: c.name,
            kind: c.kind,
            methodCount: c.methods?.length ?? 0,
            methodSample: (c.methods ?? []).slice(0, 5).map((m: any) => ({
                name: m.name,
                kind: m.kind,
                verb: m.operation?.verb ?? m.operation?.method ?? m.httpOperation?.verb,
                path: m.operation?.path ?? m.httpOperation?.path,
                parameterCount: m.parameters?.length ?? 0,
                responseName: m.response?.type?.name,
                isLro: m.kind === "lro" || m.kind === "lropaging" || !!m.__raw?.decorators?.some?.((d: any) => d.decorator?.name?.includes?.("pollingOperation")),
                // DEBUG: probe where the description hides
                _description_direct: m.description,
                _description_summary: m.summary,
                _description_raw: m.__raw?.description,
                _rawKeys: m.__raw ? Object.keys(m.__raw).slice(0, 20) : [],
                _rawKind: m.__raw?.kind,
                _rawName: m.__raw?.name,
            })),
            childCount: groups.length,
            children: depth < 3 ? groups.map((g: any) => summarizeClient(g, depth + 1, seen)) : [],
        };
    }

    // ---------- SdkContext walker (Phase A.tcgc-walk) ----------

    function deriveRpName(rootClient: any): string {
        const raw = rootClient?.name ?? "Unknown";
        return raw.replace(/Client$/, "").replace(/Management$/, "") || "Unknown";
    }

    function subClientsOf(c: any): any[] {
        return c?.subClients ?? c?.children ?? c?.operationGroups ?? [];
    }

    // Any client with methods (root or nested) becomes a Resource.
    function collectResources(rootClient: any): Resource[] {
        const out: Resource[] = [];
        const visit = (c: any, parent: Resource | null) => {
            const methods = c?.methods ?? [];
            let self: Resource | null = null;
            if (methods.length > 0) {
                const name = c.name;
                // Prefer the ACTUAL route segment from the TSP operation path
                // (e.g., "dnsZones", "signalR", "searchServices") over the
                // pluralized class name ("Zones", "SignalRs", "Services").
                // The route segment is authoritative — ARM validates against it.
                const routePlural = extractRoutePlural(methods, name);
                const plural = routePlural ?? pluralize(name);
                self = {
                    Name: singular(name),
                    Plural: plural,
                    CliLeaf: toKebab(plural),
                    KebabSingular: toKebab(singular(name)),
                    NameParam: guessNameParam(methods),
                    Parent: parent,
                    SingletonValue: null,
                    Operations: methods.map((m: any) => mapMethod(m, name)),
                };
                out.push(self);
            }
            for (const child of subClientsOf(c)) {
                visit(child, self ?? parent);
            }
        };
        visit(rootClient, null);
        return out;
    }

    // Extract the actual ARM route segment for a resource from its operation
    // paths. Returns e.g. "dnsZones" from a path like
    //   /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/dnsZones/{name}
    // by finding the segment immediately after the provider namespace. Prefers
    // item-level paths (ending in `/{param}`) over action paths (`checkNameAvailability`).
    // When we can't find a stable segment, returns undefined so callers fall
    // back to pluralize(className).
    function extractRoutePlural(methods: any[], resourceName: string): string | undefined {
        // Two-pass: first prefer item-level paths where the segment we pick is
        // followed by a path parameter (e.g. `/dnsZones/{name}`). These are the
        // canonical resource URLs. If none exist, fall back to the first segment
        // found in any operation path.
        const rn = singular(resourceName).toLowerCase();

        function tryPath(path: string, requireItemForm: boolean): string | undefined {
            // Match /providers/<ns>/<segment>[/{param}]
            const item = /\/providers\/[^/]+\/([^/{}]+)\/\{[^/]+\}/.exec(path);
            if (item) return item[1];
            if (requireItemForm) return undefined;
            const anySeg = /\/providers\/[^/]+\/([^/{}]+)/.exec(path);
            return anySeg?.[1];
        }

        // Pass 1: prefer item-level segments where the plural roughly matches
        // singular(resourceName). Handles the common case reliably.
        for (const m of methods) {
            const path = m.operation?.path ?? m.httpOperation?.path ?? "";
            const seg = tryPath(path, /*requireItemForm*/ true);
            if (seg && singular(seg).toLowerCase() === rn) return seg;
        }
        // Pass 2: any item-level segment (used when class name doesn't match).
        for (const m of methods) {
            const path = m.operation?.path ?? m.httpOperation?.path ?? "";
            const seg = tryPath(path, /*requireItemForm*/ true);
            if (seg) return seg;
        }
        // Pass 3: fallback — any provider-scoped segment. Only reached for
        // action-only resources (rare). Better than nothing.
        for (const m of methods) {
            const path = m.operation?.path ?? m.httpOperation?.path ?? "";
            const seg = tryPath(path, /*requireItemForm*/ false);
            if (seg) return seg;
        }
        return undefined;
    }

    function mapMethod(m: any, resourceName: string): Operation {
        const httpOp = m.operation ?? m.httpOperation ?? {};
        const verb = String(httpOp.verb ?? httpOp.method ?? "get").toUpperCase();
        const path = httpOp.path ?? "";
        const isLro = m.kind === "lro" || m.kind === "lropaging";
        const isPaging = m.kind === "paging" || m.kind === "lropaging";

        const pathSlots = extractPathSlots(path);
        const bodyParams = extractBodyParams(m);

        const opId = m.__raw?.name ? `${resourceName}_${capitalize(m.name)}` : `${resourceName}_${capitalize(m.name)}`;

        // Pull @doc from the tsp method. TCGC's SdkMethod doesn't expose doc
        // strings directly; call getDoc() on the underlying TypeSpec node.
        let description = m.description ?? m.summary ?? "";
        if (!description && m.__raw) {
            try {
                const program = (globalThis as any).__azclips_program;
                if (program) {
                    description = (getDoc(program, m.__raw) ?? getSummary(program, m.__raw) ?? "") as string;
                }
            } catch { /* keep empty */ }
        }

        return {
            Kind: classifyKind(verb, m.name, isPaging),
            HttpMethod: verb,
            Path: path,
            OperationId: opId,
            Description: description,
            IsLro: isLro,
            LroFinalVia: isLro ? (m.lroMetadata?.finalStateVia ?? "") : "",
            ActionName: extractActionName(path, m.name),
            Depth: 1,
            PathSlots: pathSlots,
            BodyParams: bodyParams,
            ProcessorClass: "",
            Verb: "",
            ResourceLabel: singular(resourceName),
            CmdletClass: "",
            PsInvocationName: "",
            PsPublicName: "",
            CliInvocationName: "",
            ResponseClass: m.response?.type?.name ?? "",
            ExtraUsings: [],
            RequestClass: "",
            UrlTemplate: path,
            IsSimpleParentOp: isSimpleParentOp(path),
            HasRg: /\{resourceGroupName\}/.test(path),
            HasName: /\{[a-zA-Z]+Name\}$/.test(path),
        };
    }

    function classifyKind(verb: string, methodName: string, isPaging: boolean): string {
        if (isPaging) return "list";
        const v = verb.toUpperCase();
        if (v === "GET") return "get";
        if (v === "DELETE") return "delete";
        if (v === "PUT") {
            const n = methodName.toLowerCase();
            // "createOrUpdate" / "createOrReplace" → create; explicit "update" → update
            return (n === "update" || n === "put") ? "update" : "create";
        }
        if (v === "PATCH") return "update";
        if (v === "POST") return "action";
        return "action";
    }

    function extractPathSlots(path: string): PathSlot[] {
        // Extract {placeholder} tokens along with the preceding path segment,
        // so we can disambiguate ambiguous names like {name} across nested paths.
        //
        // Example: `/redis/{name}/linkedServers/{linkedServerName}` used to produce
        // slots [{PsName:"Name"}, {PsName:"LinkedServerName"}] — the first collides
        // with the terminal Name that cmdlet.scriban always hardcodes.
        //
        // We now key non-terminal placeholders off the preceding literal segment:
        //   redis/{name}            → RedisName
        //   linkedServers/{linkedServerName} → LinkedServerName (unchanged)
        // The terminal `Name` in the template still wins for the leaf slot,
        // so we only apply the parent-prefix rewrite to non-terminal slots.
        const tokens = [...path.matchAll(/([a-zA-Z0-9_]+)\/\{([a-zA-Z0-9_]+)\}/g)];
        const slots: PathSlot[] = [];
        for (let i = 0; i < tokens.length; i++) {
            const parent = tokens[i][1];
            const name = tokens[i][2];
            const isTerminal = i === tokens.length - 1;
            const rawPs = pascalCase(name);

            let psName = rawPs;
            // Rewrite non-terminal `Name` placeholders to `<Parent>Name` for uniqueness.
            if (!isTerminal && rawPs === "Name") {
                psName = pascalCase(singularize(parent)) + "Name";
            }
            slots.push({
                PsName: psName,
                CsType: "string",
                Description: `The ${name}.`,
            });
        }
        return slots;
    }

    // Cheap English singularizer for path segments (redis stays redis; caches → cache;
    // linkedServers → linkedServer). Only used to build parent-qualified names.
    // Words ending in "is" (redis, analysis, iris) are already singular in
    // English morphology — we don't strip them.
    function singularize(s: string): string {
        if (s.endsWith("is")) return s;
        if (s.endsWith("ies")) return s.slice(0, -3) + "y";
        if (s.endsWith("ses")) return s.slice(0, -2);
        if (s.endsWith("s") && !s.endsWith("ss")) return s.slice(0, -1);
        return s;
    }

    function extractBodyParams(m: any): Param[] {
        const out: Param[] = [];
        const raw = m.parameters ?? [];
        for (const p of raw) {
            if (p.onClient) continue;
            if (p.isApiVersionParam) continue;
            if (p.isEndpointParam) continue;
            if (isPathBoundParam(p, m)) continue;
            // Filter well-known ARM URL-scope parameters even when TSP surfaces
            // them as method-level. subscription/resourceGroup slots are always
            // rendered from the cmdlet's own SubscriptionId/ResourceGroupName
            // properties (per single-op template).
            if (/^(subscriptionId|resourceGroupName|resourceGroup)$/i.test(p.name)) continue;
            if (/^(subscriptionId|resourceGroupName|resourceGroup)$/i.test(p.serializedName ?? "")) continue;

            // Filter HTTP header/query params. TCGC exposes location via a variety
            // of fields depending on version — check them all.
            const loc = p.__raw?.location ?? p.protocol?.http?.in ?? p.serializedName?.startsWith?.("Content-Type") ? "header" : undefined;
            const name = p.name;
            // Skip well-known HTTP metadata slots that leak into parameters.
            // Includes conditional-request headers (If-Match, If-None-Match, etc.)
            // that TCGC sometimes surfaces as body params depending on tsp version.
            if (/^(accept|contentType|content-Type|xmsClientRequestId|if-?match|if-?none-?match|if-?modified-?since|if-?unmodified-?since)$/i.test(name)) continue;
            if (/^(accept|contentType|content-Type|xmsClientRequestId|if-?match|if-?none-?match|if-?modified-?since|if-?unmodified-?since)$/i.test(p.serializedName ?? "")) continue;
            if (loc === "header" || loc === "query") continue;

            const type = p.type ?? {};
            const kindOfType = type.kind ?? type.__raw?.kind;
            const isModel = kindOfType === "model";
            const isArray = kindOfType === "array";
            const isDict = kindOfType === "dict";

            // Body flattening (Phase B2/B3): when the parameter is a model envelope,
            // recursively expand its properties into leaf-level Params AND record
            // each leaf's dotted JSON path so the processor can reassemble the
            // nested request body ARM expects.
            if (isModel) {
                const flattened = flattenModelParam(type, /*prefix*/ "", /*jsonPathPrefix*/ "", /*forceOptional*/ p.optional === true);
                out.push(...flattened);
                continue;
            }

            // Skip array/dict-of-model at the top level — we can't flatten a
            // collection of complex objects into scalar PS params. Users provide
            // these via `-BodyJsonPatch '{...}'`. Emitting them as `List<UnknownModel>`
            // would create unresolved type references at compile time.
            if (isArray && collectionLeafIsModel(type)) continue;
            if (isDict && collectionLeafIsModel(type)) continue;

            const csType = mapCsType(type);
            const psName = renamePsCmdletCollision(pascalCase(p.name), "");
            out.push({
                PsName: psName,
                CliName: toKebab(psName),
                CsType: csType,
                PsTypeMetadata: `typeof(${stripNullable(csType)})`,
                Description: p.description ?? "",
                Required: !p.optional,
                BodySlot: p.optional ? "body-optional" : "body-required",
                Hidden: false,
                IsPassword: hasSecretDecorator(p) || isPasswordName(p.name),
                JsonPath: p.name,   // non-model body params sit at the root
            });
        }
        // Dedupe by PsName (nested models can collide on generic names like "Name").
        const seen = new Set<string>();
        return out.filter((p) => {
            if (seen.has(p.PsName)) return false;
            seen.add(p.PsName);
            return true;
        });
    }

    // PSCmdlet base class has instance properties named Host, Path, Events, etc.
    // Any generated body param with the same name would need `new` modifier;
    // simpler: prefix it with the parent noun. When no prefix is available,
    // append "Value" (rare edge case).
    // Kept as top-level helper so both extractBodyParams and flattenModelParam
    // can share the logic.
    const PSCMDLET_COLLISIONS_TOP = new Set([
        "Host", "Path", "CommandRuntime", "CurrentPSTransaction",
        "Events", "History", "InvokeCommand", "InvokeProvider",
        "JobManager", "JobRepository", "MyInvocation", "ParameterSetName",
        "SessionState", "Stopping",
    ]);
    function renamePsCmdletCollision(psName: string, prefix: string): string {
        if (!PSCMDLET_COLLISIONS_TOP.has(psName)) return psName;
        if (prefix && !psName.startsWith(prefix)) return prefix + psName;
        return psName + "Value";
    }

    // ARM envelope properties that should never surface as user-facing params.
    // Filtered at every recursion depth. Provenance follows the swagger parser's
    // exclusion list.
    const OMIT_PROPS = new Set([
        "id", "name", "type", "systemData", "etag",
        "provisioningState",
    ]);
    // Known ARM sub-envelope model names where `.name` is a legitimate user
    // field (e.g. Sku.name = "Basic"|"Standard"|"Premium"). Inside these
    // models we bypass OMIT_PROPS filtering so the nested-required scalar
    // surfaces as -SkuName / -IdentityName / -PlanName.
    // Kept intentionally SHORT — only add well-known ARM patterns.
    const NESTED_NAME_ALLOWED_IN = new Set([
        "Sku", "Identity", "Plan", "ManagedServiceIdentity",
    ]);

    function isReadOnlyProp(sdkProp: any): boolean {
        // TCGC exposes visibility as a NUMERIC bitmask array from the TypeSpec
        // Lifecycle enum: Read=1, Create=2, Update=4, Delete=8, Query=16.
        // A property is read-only for our purposes (New-/Update- cmdlet input)
        // when the array is present AND does not include Create (2) nor Update (4).
        // Normal writable props default to [1, 2, 4, 8, 16] (all stages).
        // Explicit @visibility(Lifecycle.Read) yields [1].
        const vis = sdkProp?.visibility;
        if (Array.isArray(vis) && vis.length > 0) {
            const hasWrite = vis.some((v: any) => v === 2 || v === 4 || v === "create" || v === "update");
            if (!hasWrite) return true;
        }
        // Legacy string form (older TCGC): array of "read"/"create"/etc.
        if (Array.isArray(vis)) {
            if (vis.length === 1 && vis[0] === "read") return true;
            if (vis.every((v: string) => v === "read")) return true;
        }
        if (sdkProp?.readOnly === true) return true;
        if (sdkProp?.__raw?.readOnly === true) return true;
        return false;
    }

    function flattenModelParam(modelType: any, prefix: string, jsonPathPrefix: string, forceOptional: boolean, depth: number = 0, visited: Set<any> = new Set()): Param[] {
        const results: Param[] = [];

        // Guard against recursive types: cap recursion depth AND skip nodes we've
        // already visited on THIS path. Some Azure specs (e.g. Network's VirtualNetwork
        // → Subnet → NSG → SecurityRules → ... circles back) exceed JavaScript's
        // default stack size otherwise.
        //
        // CRITICAL: the visited set MUST be per-path (cloned at each recursion), not
        // shared across siblings. Sharing collapses two independent uses of the same
        // model type into one — e.g. `wireServer?: HostEndpointSettings; imds?:
        // HostEndpointSettings;` would only emit fields for the first branch.
        if (depth > 20) return results;
        if (visited.has(modelType)) return results;
        const localVisited = new Set(visited);
        localVisited.add(modelType);

        // Detect if this model is a known ARM sub-envelope where OMIT_PROPS
        // exceptions apply (e.g. Sku.name is a real user field, not ARM metadata).
        const modelName = modelType.name ?? modelType.__raw?.name ?? "";
        const isNestedNameAllowed = NESTED_NAME_ALLOWED_IN.has(modelName);

        // Walk parent (baseModel) first so its properties appear before this
        // model's. Handles ARM's TrackedResource → Location/Tags on every resource
        // envelope.
        const base = modelType.baseModel ?? modelType.parent ?? modelType.__raw?.baseModel;
        if (base && base !== modelType) {
            results.push(...flattenModelParam(base, prefix, jsonPathPrefix, forceOptional, depth + 1, localVisited));
        }

        // Strip common noise suffixes from the incoming prefix. Azure specs love
        // "Details" wrappers (MarketplaceDetails, UserDetails). We keep the domain
        // noun but drop the suffix.
        const effectivePrefix = stripPrefixNoise(prefix);

        const props = modelType.properties ?? [];
        for (const prop of props) {
            const name = prop.name ?? prop.serializedName ?? "";
            if (!name) continue;
            // OMIT_PROPS filter — with narrow exception: inside a known
            // sub-envelope (Sku/Identity/Plan), `.name` is a legitimate user
            // field, so let it through.
            if (OMIT_PROPS.has(name)) {
                const isAllowedSubEnvelopeName = isNestedNameAllowed && name === "name";
                if (!isAllowedSubEnvelopeName) continue;
            }
            // Filter well-known ARM URL-scope names even when TSP defines them
            // on a body model (e.g. CheckNameAvailability's `resourceGroupName`
            // request field — the URL slot property does the real routing, we
            // don't want to double-declare and shadow).
            if (/^(subscriptionId|resourceGroupName|resourceGroup)$/i.test(name)) continue;
            if (isReadOnlyProp(prop)) continue;

            const propType = prop.type ?? {};
            const propKind = propType.kind ?? propType.__raw?.kind;

            // ARM envelope's "properties" is transparent to the CLI param name but
            // is a real key in the JSON body — reflect it in jsonPathPrefix only.
            const promoteThroughProperties = name === "properties" && propKind === "model";
            if (promoteThroughProperties) {
                results.push(...flattenModelParam(propType, effectivePrefix, jsonPathPrefix + "properties.", forceOptional, depth + 1, localVisited));
                continue;
            }

            // ARM envelope's "tags" is by convention a Dict<string,string>. Emit
            // it as a Hashtable at the top level (never prefixed).
            if (name === "tags" && (propKind === "dict")) {
                results.push({
                    PsName: "Tags",
                    CliName: "tags",
                    CsType: "Dictionary<string, string>?",
                    PsTypeMetadata: "typeof(object)",
                    Description: prop.description ?? "Resource tags.",
                    Required: false,
                    BodySlot: "body-optional",
                    Hidden: false,
                    IsPassword: false,
                    JsonPath: jsonPathPrefix + "tags",
                });
                continue;
            }

            // Skip Dict/List of *model* — those don't flatten cleanly.
            // Also skip nested collection types (List<List<Model>>, Dict<K, List<Model>>)
            // — anything whose leaf ultimately references a model. Emitting them
            // as `List<UnknownModel>` creates unresolved type refs at compile time.
            if (propKind === "dict" && collectionLeafIsModel(propType)) continue;
            if (propKind === "array" && collectionLeafIsModel(propType)) continue;

            // Nested model → walk into its properties with our name as a prefix.
            if (propKind === "model") {
                const nestedPrefix = effectivePrefix + pascalCase(name);
                results.push(...flattenModelParam(propType, nestedPrefix, jsonPathPrefix + name + ".", forceOptional || prop.optional === true, depth + 1, localVisited));
                continue;
            }

            // Leaf property → emit a single Param.
            const csType = mapCsType(propType);
            const stripped = stripNullable(csType);
            // Only treat as a SecureString-carrying param when the underlying type
            // is actually a string. Field names like `disablePasswordAuthentication`
            // are booleans and would break the SecureString cast otherwise.
            const isStringLeaf = stripped === "string";
            const isPwd = isStringLeaf && (hasSecretDecorator(prop) || isPasswordName(name));
            const req = !prop.optional && !forceOptional;
            const rawName = pascalCase(name);
            let psName = (effectivePrefix && rawName.startsWith(effectivePrefix))
                ? rawName
                : (effectivePrefix + rawName);
            // Rename PSCmdlet base-class member collisions (Host, Path, etc.).
            psName = renamePsCmdletCollision(psName, effectivePrefix);
            results.push({
                PsName: psName,
                CliName: toKebab(psName),
                CsType: req ? stripNullable(csType) : (csType.endsWith("?") ? csType : csType + "?"),
                PsTypeMetadata: `typeof(${stripped})`,
                Description: prop.description ?? "",
                Required: req,
                BodySlot: req ? "body-required" : "body-optional",
                Hidden: false,
                IsPassword: isPwd,
                JsonPath: jsonPathPrefix + name,
            });
        }
        return results;
    }

    function stripPrefixNoise(prefix: string): string {
        if (!prefix) return prefix;
        // Drop "Details" suffix that Azure specs use for wrapper models
        // (MarketplaceDetails, UserDetails) but keep the domain noun.
        return prefix.replace(/Details$/, "");
    }

    // True if a Dict/List type (possibly nested Dict<K, List<T>> or
    // List<List<T>>) ultimately contains a model at any depth. Used to skip
    // params whose emitted C# type would reference an unresolved model class.
    function collectionLeafIsModel(t: any, depth: number = 0): boolean {
        if (!t || depth > 8) return false;
        const k = t.kind ?? t.__raw?.kind;
        if (k === "model") return true;
        if (k === "array" || k === "dict") {
            return collectionLeafIsModel(t.valueType, depth + 1);
        }
        return false;
    }

    function isPathBoundParam(p: any, m: any): boolean {
        const path = m.operation?.path ?? m.httpOperation?.path ?? "";
        return new RegExp(`\\{${p.name}\\}`).test(path);
    }

    function hasSecretDecorator(p: any): boolean {
        const raw = p.__raw ?? p.type?.__raw;
        const decs = raw?.decorators ?? [];
        return decs.some?.((d: any) => d.decorator?.name?.endsWith?.("$secret") || d.definition?.name === "@secret");
    }

    function isPasswordName(name: string): boolean {
        return /(password|secret|apiKey|accessKey)/i.test(name);
    }

    function mapModel(model: any): ModelClass {
        const props: ModelProp[] = (model.properties ?? []).map((p: any) => {
            const base = mapCsType(p.type);
            // Optional props must be nullable so generated code compiles with
            // <Nullable>enable</Nullable>. Model refs land without a trailing ?
            // from mapCsType; add it here when the prop itself is optional.
            const cs = p.optional && !base.endsWith("?") ? base + "?" : base;
            return {
                Name: pascalCase(p.name),
                CsType: cs,
                Required: !p.optional,
                XmlSummary: p.description ?? "",
            };
        });
        return {
            Name: model.name,
            Props: props,
            XmlSummary: (model as any).description ?? "",
        };
    }

    function mapCsType(type: any): string {
        if (!type) return "string?";
        const k = type.kind ?? type.__raw?.kind;
        switch (k) {
            case "string":
            case "url":
            case "uuid":
            case "guid":
            case "azureLocation":
            case "duration":
            case "utcDateTime":
            case "offsetDateTime":
                return "string?";
            case "int32":
            case "int8":
            case "int16":
                return "int?";
            case "int64":
                return "long?";
            case "float32":
                return "float?";
            case "float64":
            case "decimal":
            case "decimal128":
                return "double?";
            case "boolean":
                return "bool?";
            case "array":
                return `List<${stripNullable(mapCsType(type.valueType))}>`;
            case "dict":
                return `Dictionary<string, ${stripNullable(mapCsType(type.valueType))}>`;
            case "enum":
            case "enumvalue":
                return "string?";
            case "model":
                return type.name ?? "object";
            case "constant":
                return "string?";
            default:
                return "string?";
        }
    }

    function stripNullable(t: string): string {
        return t.endsWith("?") ? t.slice(0, -1) : t;
    }

    function detectProviderNamespace(resources: Resource[]): string {
        for (const r of resources) {
            for (const op of r.Operations) {
                const m = op.Path.match(/\/providers\/([^/]+)\//);
                if (m) return m[1];
            }
        }
        return "";
    }

    function isSimpleParentOp(path: string): boolean {
        return /^\/subscriptions\/\{subscriptionId\}\/resourceGroups\/\{resourceGroupName\}\/providers\/[^/]+\/[^/]+(\/\{[^/]+\})?$/.test(path);
    }

    function guessNameParam(methods: any[]): string {
        for (const m of methods) {
            const path = m.operation?.path ?? "";
            const seg = path.split("/").filter(Boolean).pop();
            const nm = /^\{([a-zA-Z]+)Name\}$/.exec(seg ?? "");
            if (nm) return nm[1] + "Name";
        }
        return "name";
    }

    function extractActionName(path: string, methodName: string): string {
        // POST /providers/.../resource/{name}/actionName  → "actionName"
        const parts = path.split("/").filter(Boolean);
        const last = parts[parts.length - 1];
        if (last && !last.startsWith("{")) return last;
        return "";
    }

    // ---------- Naming helpers ----------

    function stripODataPrefix(s: string): string {
        // Strip leading '$' from OData query params ($top, $skip, $filter,
        // $expand, $search, $select, $count) so they become valid C#
        // identifiers (Top / Skip / Filter / Expand / Search / Select / Count).
        return s.startsWith("$") ? s.slice(1) : s;
    }

    function pascalCase(s: string): string {
        s = stripODataPrefix(s);
        if (!s) return "";
        return s
            .replace(/[_\-\s]+(.)/g, (_, c) => c.toUpperCase())
            .replace(/^(.)/, (_, c) => c.toUpperCase());
    }

    function toKebab(s: string): string {
        return stripODataPrefix(s)
            .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
            .replace(/([A-Z]+)([A-Z][a-z])/g, "$1-$2")
            .toLowerCase();
    }

    function capitalize(s: string): string {
        return s ? s[0].toUpperCase() + s.slice(1) : "";
    }

    function singular(s: string): string {
        if (s.endsWith("ies")) return s.slice(0, -3) + "y";
        if (s.endsWith("ses") || s.endsWith("xes")) return s.slice(0, -2);
        if (s.endsWith("s") && !s.endsWith("ss")) return s.slice(0, -1);
        return s;
    }

    function pluralize(s: string): string {
        // If already plural (ends in 's' and not "ss"), leave it.
        if (s.endsWith("s") && !s.endsWith("ss")) return s;
        if (s.endsWith("y") && !/[aeiou]y$/i.test(s)) return s.slice(0, -1) + "ies";
        if (/(x|z|ch|sh)$/i.test(s)) return s + "es";
        return s + "s";
    }

