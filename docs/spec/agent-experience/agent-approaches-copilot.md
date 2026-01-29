# Cirrus Handler Integration with Azure Client Tools Copilot

**Status:** Design Document  
**Date:** January 2026  
**Owner:** Cirrus Team  
**Reviewers:** Azure Client Tools Copilot Team (Yu Chen, Zelin Wang)

---

## Purpose

Replace the current local OpenAI-based agent (`CloudAgent.cs`) with a production-ready handler integrated into the Azure Client Tools Copilot infrastructure. This leverages proven architecture, shared knowledge management, and enables cross-product reuse.

## Current State

**Cirrus Today:**
- Local agent running in-process with embedded OpenAI API calls
- API keys embedded in code (security concern)
- Prompt template stored locally in `prompt.md`
- No validation/evaluation loop
- No automatic knowledge base updates
- Single-use, not reusable by other products

**Azure Client Tools Copilot Infrastructure:**
- Production handlers for CLI, PowerShell, Terraform
- Remote API-based architecture with security solved
- Knowledge base with automatic updates (CLI uses Microsoft Learn API)
- Validation service for command syntax
- Evaluation loop with retries for quality
- Reusable by multiple products (Portal, VS Code, GitHub Copilot for Azure)

## Proposed Architecture

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              User Device                                    │
│                                                                             │
│  ┌────────────────┐              ┌──────────────────┐                       │
│  │  Cirrus CLI    │              │ Cirrus PowerShell│                       │
│  │    (azv2)      │              │  (Invoke-AzAI)   │                       │
│  └────────┬───────┘              └────────┬─────────┘                       │
│           │                               │                                 │
│           │         ┌────────────────────────────┐                          │
│           └─────────┤  CloudAgent.cs (HTTP)      │                          │
│                     │  - Builds request          │                          │
│                     │  - Manages conversation    │                          │
│                     │  - Handles JWT token       │                          │
│                     └────────────┬───────────────┘                          │
└──────────────────────────────────┼──────────────────────────────────────────┘
                                   │
                                   │ HTTPS POST
                                   │ (JWT token in header)
                                   │
┌──────────────────────────────────┼──────────────────────────────────────────┐
│                                  │  Azure Client Tools Copilot              │
│                                  ▼  Infrastructure                          │
│                     ┌────────────────────────┐                              │
│                     │   API Gateway          │                              │
│                     │   - JWT validation     │                              │
│                     │   - Rate limiting      │                              │
│                     │   - Routing            │                              │
│                     └────────────┬───────────┘                              │
│                                  │                                          │
│                                  ▼                                          │
│                     ┌────────────────────────┐                              │
│                     │  Cirrus Handler        │                              │
│                     │  - Parse request       │◄────────┐                    │
│                     │  - Build prompt        │         │                    │
│                     │  - Orchestrate flow    │         │                    │
│                     │  - Retry logic         │         │ Retry with         │
│                     └────────┬───────────────┘         │ feedback           │
│                              │                         │                    │
│              ┌───────────────┼──────────────┐          │                    │
│              │               │              │          │                    │
│              ▼               ▼              ▼          │                    │
│  ┌──────────────────┐  ┌──────────────┐  ┌─────────────────┐                │
│  │  Azure AI Search │  │   Azure      │  │   Validation    │                │
│  │  (Knowledge Base)│  │   OpenAI     │  │   Service       │─────────────   |
│  │                  │  │   (LLM)      │  │  - Parse command│                │
│  │  - Command ref   │  │              │  │  - Verify syntax│                │
│  │  - Examples      │  │  - GPT-4o    │  │  - Return errors│                │
│  │  - Docs          │  │  - Streaming │  │                 │                │
│  └──────────────────┘  └──────────────┘  └─────────────────┘                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────┐
│                         Cirrus CICD Pipeline                                 │
│                                                                              │
│  ┌──────────────┐      ┌──────────────────┐      ┌──────────────────┐        │
│  │  Build &     │      │  Extract Command │      │  Update Azure AI │        │
│  │  Deploy      │─────▶│  Metadata        │─────▶│  Search Index    │        │
│  │  Cirrus      │      │  - Help text     │      │  (Knowledge Base)│        │
│  │              │      │  - Examples      │      │                  │        │
│  └──────────────┘      └──────────────────┘      └──────────────────┘        │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Handler Location

Add Cirrus as a **fourth handler** in the Azure Client Tools Copilot service:

```
Existing:
- CLI Handler (uses Microsoft Learn docs)
- PowerShell Handler (own knowledge base + Azure AI Search)
- Terraform Handler (own knowledge base + Azure AI Search)

New:
- Cirrus Handler (own knowledge base + Azure AI Search)
```

### Reuse Strategy

**Reuse from existing infrastructure:**
- Deployment pipeline and infrastructure
- Security/authentication mechanisms
- API gateway and JWT token validation
- Observability/telemetry framework
- Validation service architecture
- Retry/evaluation loop patterns

**Build new for Cirrus:**
- Cirrus-specific handler endpoint
- Cirrus command knowledge base
- Custom prompt template for Cirrus scenarios
- CICD integration for knowledge base updates

## Key Components

### 1. Cirrus Handler Service

**Purpose:** Handle natural language requests for Cirrus commands

**Behavior differences from existing handlers:**
- Support multiple interaction scenarios (not just script generation):
  - Typo correction
  - How-to questions  
  - Natural language execution
  - Troubleshooting
  - Guidance
- Return both commands and conversational responses
- Maintain conversation history across turns

**Interface:** REST API endpoint callable by Cirrus CLI/PowerShell

### 2. Cirrus Knowledge Base

**Technology:** Azure AI Search (similar to PowerShell/Terraform approach)

**Content source:** Auto-generated from Cirrus codebase and documentation

**Update mechanism:** Integrated into Cirrus CICD pipeline
- On every production/dev deployment
- Extracts command metadata, help text, examples
- Updates Azure AI Search index
- Takes ~2 hours (based on PowerShell team experience)

**Structure:**
- Command reference (syntax, parameters, examples)
- Conceptual documentation
- Common patterns and scenarios
- Troubleshooting guides

### 3. Validation Service Integration

**Purpose:** Verify generated commands are syntactically correct

**Approach:** Leverage existing validation service pattern but adapt for Cirrus

**Process:**
1. Handler generates Cirrus command
2. Validation service checks against Cirrus command schema
3. If invalid, retry with feedback (up to 3 times)
4. Return validated command to client

### 4. Cirrus Client Integration

**Replace:** `src/Core/Core.Agent/CloudAgent.cs`

**With:** HTTP client that calls Cirrus handler endpoint

**Authentication:** JWT token from Cirrus login flow

**Request/Response:**
- Request: User input + conversation history + context (subscription, tenant, etc.)
- Response: JSON with command/response + type indicator

## Integration Points

### Cirrus → Handler (Request Flow)

```
┌─────────────┐
│   User      │
└──────┬──────┘
       │ "create a resource group called mygroup in westus"
       ▼
┌──────────────────────────────────────────────────────────────┐
│  Cirrus CLI/PowerShell                                       │
│                                                              │
│  1. User input received                                      │
│  2. Build request payload:                                   │
│     {                                                        │
│       "userInput": "create a resource group...",             │
│       "conversationHistory": [...previous turns...],         │
│       "context": {                                           │
│         "subscriptionId": "xxx",                             │
│         "tenantId": "yyy",                                   │
│         "cirrusVersion": "2.0.0"                             │
│       }                                                      │
│     }                                                        │
│  3. Get JWT token from auth context                          │
│  4. HTTP POST to handler                                     │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            │ HTTPS POST
                            │ Authorization: Bearer <JWT>
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  API Gateway                                                 │
│                                                              │
│  1. Validate JWT token                                       │
│  2. Check Copilot permission (via permission API)            │
│  3. Rate limiting check                                      │
│  4. Route to Cirrus handler                                  │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Cirrus Handler                                              │
│                                                              |
│  1. Parse request                                            │
│  2. Build augmented prompt:                                  │
│     - System instructions (Cirrus scenarios)                 │
│     - User input                                             │
│     - Conversation history                                   │
│     - Context (subscription, tenant)                         │
│                                                              │
│  3. Query knowledge base (Azure AI Search)                   │
│     ├─ Search for relevant commands                          │
│     ├─ Get examples and documentation                        │
│     └─ Inject into prompt as context                         │
│                                                              │
│  4. Call Azure OpenAI (LLM)                                  │
│     ├─ Stream response                                       │
│     └─ Accumulate complete response                          │
│                                                              │
│  5. Parse LLM response (extract command if present)          │
│                                                              │
│  6. If command generated → Validate                          │
│     ├─ Call validation service                               │
│     ├─ If invalid → Retry (max 3 times)                      │
│     │   └─ Include validation error in next LLM call         │
│     └─ If valid → Continue                                   │
│                                                              │
│  7. Build response:                                          │
│     {                                                        │
│       "type": "executable_command",                          │
│       "response": "I'll create a resource group...",         │
│       "command": "azv2 group create --name mygroup...",      │
│       "confidence": 0.95                                     │
│     }                                                        │
│                                                              │
│  8. Return to client                                         │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            │ HTTP 200 + JSON response
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Cirrus Client (CloudAgent.cs replacement)                   │
│                                                              │
│  1. Receive response                                         │
│  2. Add to conversation history                              │
│  3. Interpret response type:                                 │
│     - executable_command → Prompt for confirmation           │
│     - sample_command → Display help                          │
│     - question → Display question, await user input          │
│     - exit → Terminate conversation                          │
│  4. Execute command if confirmed                             │
│  5. Display result                                           │
└──────────────────────────────────────────────────────────────┘
```

**Call pattern:**
1. User enters natural language in Cirrus CLI/PowerShell
2. Cirrus client constructs request with context
3. HTTP POST to Cirrus handler endpoint
4. Handler processes request (LLM + knowledge base + validation)
5. Handler returns structured response
6. Cirrus client interprets response and executes/displays

**Context passed:**
- User input
- Conversation history (for multi-turn conversations)
- Current subscription ID
- Current tenant ID
- Cirrus version

### Cirrus CICD → Knowledge Base

```
┌──────────────────────────────────────────────────────────────┐
│  Cirrus CICD Pipeline (Production/Dev Deployment)            │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            │ Post-deployment hook
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Knowledge Base Update Script                                │
│                                                              │
│  1. Load built Cirrus assemblies                             │
│     ├─ Load DLLs from artifacts/Release                      │
│     └─ Use reflection to discover commands                   │
│                                                              │
│  2. Extract command metadata:                                │
│     For each command:                                        │
│     ├─ Command name and aliases                              │
│     ├─ Parameter names, types, required/optional             │
│     ├─ Help text and descriptions                            │
│     ├─ Examples from attributes/XML docs                     │
│     └─ Related commands and workflows                        │
│                                                              │
│  3. Load documentation files:                                │
│     ├─ README.md files                                       │
│     ├─ docs/spec/*.md                                        │
│     ├─ docs/howto/*.md                                       │
│     └─ Inline XML documentation                              │
│                                                              │
│  4. Generate search index documents:                         │
│     {                                                        │
│       "id": "command-group-create",                          │
│       "type": "command",                                     │
│       "command": "group create",                             │
│       "syntax": "azv2 group create --name <name> ...",       │
│       "description": "Creates a resource group...",          │
│       "parameters": [...],                                   │
│       "examples": [...],                                     │
│       "relatedCommands": [...],                              │
│       "lastUpdated": "2026-01-29T12:00:00Z",                 │
│       "cirrusVersion": "2.0.0"                               │
│     }                                                        │
│                                                              │
│  5. Backup current index (for rollback)                      │
│                                                              │
│  6. Update Azure AI Search index:                            │
│     ├─ Delete obsolete documents                             │
│     ├─ Update modified documents                             │
│     ├─ Add new documents                                     │
│     └─ Refresh index                                         │
│                                                              │
│  7. Verify index health:                                     │
│     ├─ Run sample queries                                    │
│     ├─ Check document count                                  │
│     └─ Validate search scoring                               │
│                                                              │
│  8. Log metrics:                                             │
│     ├─ Documents added/updated/deleted                       │
│     ├─ Update duration                                       │
│     └─ Any errors or warnings                                │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            │ Success/Failure status
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Azure AI Search (Knowledge Base)                            │
│                                                              │
│  Updated index ready for queries from Cirrus handler         │
└──────────────────────────────────────────────────────────────┘
```

**Trigger:** Production/dev deployment

**Process:**
1. Post-deployment script runs
2. Extracts command metadata from built assemblies
3. Generates documentation content
4. Updates Azure AI Search index
5. Logs update status/metrics

**Duration:** ~2 hours (based on PowerShell team experience)

**Rollback:** Previous index version maintained for safety

### Handler → Validation Service

```
┌──────────────────────────────────────────────────────────────┐
│  Cirrus Handler                                              │
│                                                              │
│  LLM generated command:                                      │
│  "azv2 group create --name mygroup --location westus"        │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            │ Validation request
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Validation Service                                          │
│                                                              │
│  1. Receive command string                                   │
│                                                              │
│  2. Parse command using Cirrus command parser:               │
│     ├─ Tokenize command line                                 │
│     ├─ Match command: "group create"                         │
│     ├─ Extract parameters: name=mygroup, location=westus     │
│     └─ Validate against command schema                       │
│                                                              │
│  3. Validation checks:                                       │
│     ├─ Command exists?                                       │
│     ├─ Required parameters present?                          │
│     ├─ Parameter types correct?                              │
│     ├─ Parameter values valid?                               │
│     ├─ No unknown parameters?                                │
│     └─ Syntax correct?                                       │
│                                                              │
│  4. Build validation result:                                 │
│                                                              │
│     SUCCESS:                                                 │
│     {                                                        │
│       "valid": true,                                         │
│       "command": "group create",                             │
│       "parameters": {                                        │
│         "name": "mygroup",                                   │
│         "location": "westus"                                 │
│       }                                                      │
│     }                                                        │
│                                                              │
│     FAILURE:                                                 │
│     {                                                        │
│       "valid": false,                                        │
│       "errors": [                                            │
│         {                                                    │
│           "parameter": "location",                           │
│           "error": "Invalid location 'westus2'",             │
│           "suggestion": "Valid locations: westus, eastus..." │
│         }                                                    │
│       ]                                                      │
│     }                                                        │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            │ Validation result
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Cirrus Handler                                              │
│                                                              │
│  If valid:                                                   │
│    └─ Return command to client                               │
│                                                              │
│  If invalid (attempt < 3):                                   │
│    ├─ Build retry prompt with error feedback:                │
│    │  "The command is invalid. Error: Invalid location       │
│    │   'westus2'. Valid locations: westus, eastus...         │
│    │   Please generate a corrected command."                 │
│    └─ Call LLM again with feedback                           │
│                                                              │
│  If invalid (attempt >= 3):                                  │
│    └─ Return error to client                                 │
└──────────────────────────────────────────────────────────────┘
```

**Trigger:** Command generated by LLM

**Process:**
1. Handler sends generated command to validation service
2. Validation service parses command using Cirrus command parser
3. Returns validation result (valid/invalid + error details)
4. If invalid, handler retries with error feedback
5. Maximum 3 retry attempts

## Implementation Considerations

### Authentication & Authorization

**Logged-in users:**
- Use JWT token from Cirrus login
- Validate token via existing copilot permission API
- Full access to handler capabilities

**Anonymous users (not logged in):**
- **Proposed:** Limited access (3 requests)
- After limit: Prompt to log in with helpful messaging
- No access to resource operations (read-only guidance only)
- Alternative: Regex-based responses ("You must log in first")

**Decision needed:** Finalize anonymous user approach

### Performance

**Current handler concerns:** Can be slow due to validation loops

**Optimization opportunities:**
- Use faster LLM deployment for Cirrus (if quality acceptable)
- Reduce retry attempts for simple queries
- Cache common queries/responses
- Pre-warm knowledge base searches
- Consider agentic framework for token/retry management

**Monitor:** Add observability to track response times by scenario

### Extensibility

**Design for reuse by other products:**
- Other Azure agents can call Cirrus handler
- Useful for troubleshooting, guidance, command completion
- Knowledge base parameter: Support multiple knowledge bases in same handler
- Example: Same code, different knowledge base for Cirrus vs. legacy CLI

### Conversation History

**Requirement:** Multi-turn conversations (question → answer → follow-up)

**Approach:**
- Client maintains conversation history
- Passes history array with each request
- Handler uses history for context
- Consider conversation TTL/limits to manage token count

## Open Questions

### 1. Anonymous User Access
- **Option A:** 3 free requests, then require login
- **Option B:** No access, always require login
- **Option C:** Regex-based fallback responses only

**Recommendation:** Option A (3 free requests) - allows discovery without friction

### 2. Knowledge Base Maintenance
- Who owns the CICD integration? (Cirrus team or Copilot team)
- How to handle breaking changes in command schema?
- Versioning strategy for knowledge base?

### 3. Performance Targets
- What's acceptable latency for first token? (Currently 300-800ms)
- Should we have faster/less-accurate mode for simple queries?

### 4. Deployment
- Deploy in Copilot infrastructure or separate resource group?
- Share LLM deployment or dedicated Cirrus deployment?
- Staging environment strategy?

### 5. Agentic Framework
- Should we use an agentic framework (e.g., Semantic Kernel, LangChain)?
- Benefits: Built-in retry, token management, conversation tracking
- Trade-off: Additional dependency and learning curve

**Recommendation:** Start without framework, add if needed

## Success Criteria

- [ ] Cirrus handler deployed and operational in Copilot infrastructure
- [ ] Knowledge base auto-updates from Cirrus CICD
- [ ] Authentication/authorization working for logged-in users
- [ ] Validation service integrated and reducing invalid commands
- [ ] Response time acceptable (< 2 seconds for simple queries)
- [ ] Other products can reuse Cirrus handler
- [ ] Security audit passed (no embedded secrets)

## Next Steps

1. **Design Review:** Team review and approval of this document
2. **Prototype:** Build minimal Cirrus handler in isolated environment (Copilot repo)
3. **Knowledge Base Setup:** Create Azure AI Search index and test population
4. **CICD Integration:** Add knowledge base update to Cirrus deployment pipeline
5. **Client Integration:** Replace CloudAgent.cs with handler HTTP client
6. **Testing:** Validate scenarios against production handlers
7. **Performance Tuning:** Optimize based on observability data
8. **Production Rollout:** Gradual rollout with monitoring

## References

- [Cirrus Agent Experience](../agent-experience.md)
- [Agent Implementation Approaches](agent-approaches.md)
- Azure Client Tools Copilot Infrastructure (internal docs)
- Meeting transcript: 2026-01-27
