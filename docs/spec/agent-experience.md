# Agent Experience

## Overview

Core.Agent is an AI-powered natural language interface for Azure CLI and Azure PowerShell that interprets user intent and generates executable commands. It operates as an **exception-driven fallback**: when standard command parsing fails, control passes to the agent to interpret the natural language input.

The agent supports three interaction scenarios:
1. **Typo Correction** - Recognizes misspelled commands and suggests correct syntax
2. **"How To" Questions** - Provides command examples with placeholders (not executable)
3. **Natural Language Execution** - Generates complete executable commands from natural language requests

**Key Behavioral Note:**
Both Azure CLI and Azure PowerShell support interactive confirmation. When the agent generates an executable command, it prompts the user for confirmation. The user can respond with natural language (e.g., "yes", "sure", "go ahead", "no", "cancel"), and the agent uses a language model to interpret their intent - not a hard-coded yes/no check.

**Note:** For comprehensive architecture details including fallback mechanisms and PowerShell subprocess integration, see [ARCHITECTURE.md](ARCHITECTURE.md).

## See Also

For detailed implementation information, see:

- ![Status](https://img.shields.io/badge/status-implemented-green) - **[Azure CLI Agent Experience](agent-experience/agent-experience-azcli.md)** - Detailed CLI-specific implementation, architecture, and examples
- ![Status](https://img.shields.io/badge/status-design%20only-yellow) - **[Azure PowerShell Agent Experience](agent-experience/agent-experience-azps.md)** - PowerShell design (initial implementation removed, pending review)
- **[Agent Implementation Approaches](agent-experience/agent-approaches.md)** - AI backend options: Azure OpenAI (implemented), Local Model (implemented), Azure Copilot Service (under review)


## Agent Capabilities

The agent supports three interaction scenarios with behavioral differences between platforms. For platform-specific details and comprehensive examples, see [agent-experience-azcli.md](agent-experience/agent-experience-azcli.md) and [agent-experience-azps.md](agent-experience/agent-experience-azps.md).

### Scenario 1: Typo Correction

**Trigger:** User enters a command with spelling errors

**Agent Behavior:** Recognizes the intent and provides correct syntax examples

**Response Type:** `sample_command` (JSON) - contains placeholders, not executable

#### Example
```bash
$ azv2 I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure CLI:
azv2 login
azv2 login --service-principal --username <appId> --password <password> --tenant <tenantId>
```

---

### Scenario 2: "How To" Questions

**Trigger:** User asks how to perform a task ("how do I...", "how can I...")

**Agent Behavior:** Provides command examples with placeholders for user-specific values

**Response Type:** `sample_command` (JSON) - contains placeholders, not executable

#### Example
```bash
$ azv2 how do I create a resource group

⠋ Thinking...

To create a resource group in Azure:

Azure CLI:
azv2 group create --name <resource-group-name> --location <location>

# Example with common locations:
azv2 group create --name myResourceGroup --location eastus
```

**Behavioral Note:** Both interfaces display guidance only - user must manually execute commands with their own values.

---

### Scenario 3: Natural Language Execution

**Trigger:** User provides complete intent with specific values ("create a resource group called X in Y")

**Agent Behavior:** Generates fully executable command with user-provided values

**Response Type:** `executable_command` (JSON) - ready to execute

#### Example (Interactive Confirmation)
```bash
$ azv2 create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
azv2 group create --name mygroup --location westus

Execute this command? (yes/no): yes

⠋ Executing...

{
  "id": "/subscriptions/.../resourceGroups/mygroup",
  "location": "westus",
  "name": "mygroup",
  "properties": {
    "provisioningState": "Succeeded"
  }
}
```

**Behavior:**
1. Agent generates complete command
2. Prompts user for confirmation (interactive)
3. User responds with natural language (e.g., "yes", "sure", "go ahead", "no", "cancel")
4. Agent interprets user intent using language model
5. If confirmed, executes the command
6. Displays command output

---

### Platform Behavioral Comparison

| Feature | Azure CLI | Azure PowerShell |
|---------|-----------|------------------|
| **Interactive Prompts** | ✅ Yes - prompts for confirmation | ✅ Yes - prompts for confirmation |
| **Confirmation** | Agent interprets natural language response | Agent interprets natural language response |
| **Command Marker** | Not needed - direct execution | `__PSEXEC__<command>` in stdout |
| **Execution Method** | Recursive azv2 call in same process | `Invoke-Expression` in parent PowerShell |
| **User Safety** | AI-interpreted confirmation with natural language | AI-interpreted confirmation with natural language |
| **Flavor Detection** | Uses "azure cli" in prompt template | Uses "azure powershell" via AZURECLI_FLAVOR |
| **Example Syntax** | `azv2 create resource group X` | `ask create resource group X` (no quotes) |
| **Status** | ✅ Implemented | 📋 Design Only |

For detailed platform-specific architecture and comprehensive examples, see:
- [Azure CLI Agent Experience](agent-experience/agent-experience-azcli.md)
- [Azure PowerShell Agent Experience](agent-experience/agent-experience-azps.md)

---


## Response Protocol

The agent communicates using structured JSON responses with a `type` field that determines how the response should be handled.

### JSON Response Types

1. **`sample_command`** - Help text with placeholders
   ```json
   {
     "type": "sample_command",
     "explanation": "Here's how to create a resource group:",
     "command": "azv2 group create --name <name> --location <location>"
   }
   ```

2. **`executable_command`** - Complete command ready to run
   ```json
   {
     "type": "executable_command",
     "explanation": "I'll create a resource group named 'mygroup'.",
     "command": "azv2 group create --name mygroup --location westus"
   }
   ```

3. **`question`** - Agent needs more information
   ```json
   {
     "type": "question",
     "question": "What location would you like for the resource group?"
   }
   ```

4. **`confirmation`** - Asks user to confirm (CLI only)
   ```json
   {
     "type": "confirmation",
     "message": "This will delete resource group 'mygroup'.",
     "command": "azv2 group delete --name mygroup"
   }
   ```

5. **`exit`** - User cancelled
   ```json
   {
     "type": "exit",
     "message": "Operation cancelled."
   }
   ```

### User-Facing Markers

- **PowerShell Execution Marker:** `__PSEXEC__<command>` in stdout triggers `Invoke-Expression`
- **Cancellation Keywords:** `cancel`, `quit`, `exit`, `stop` terminate conversations

---

## Running the Agent

### Azure CLI

The agent is automatically invoked when standard command parsing fails:

```bash
# Natural language commands
$ azv2 create a storage account called mystore in westus

# Typo correction
$ azv2 how do I logni

# Help questions
$ azv2 how do I list virtual machines
```

### Azure PowerShell

Use the `Invoke-AzAI` function or its aliases:

```powershell
# Full function name (no quotes needed)
Invoke-AzAI create a storage account called mystore in westus

# Convenient aliases
ask how do I create a resource group
please show me all storage accounts
pls list virtual machines in eastus
```

**Important:** PowerShell arguments use `ValueFromRemainingArguments`, so **do not use quotes** around queries.

For detailed platform-specific usage and examples, see:
- [Azure CLI Agent Experience](agent-experience/agent-experience-azcli.md)
- [Azure PowerShell Agent Experience](agent-experience/agent-experience-azps.md)

---

## AI Backend Approaches

The agent supports three AI backend implementations:

### Azure OpenAI (Default) - ![Status](https://img.shields.io/badge/status-implemented-green)

**Technology:** Azure OpenAI Service with GPT-4o-mini

**Configuration:**
```bash
# .env file in src/Core/Core.Agent/
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Characteristics:**
- Excellent accuracy
- 300-800ms latency to first token
- Requires internet connectivity
- Pay-per-token pricing (~$0.0001-0.0003 per request)

### Local Model - ![Status](https://img.shields.io/badge/status-implemented-green)

**Technology:** LlamaSharp with Phi-3.5-mini (~2.4GB)

**Features:**
- Automatic model download on first run
- Cached in `~/.cache/llama_models/`
- Fully offline, no external dependencies
- Zero operational cost

**Usage:**
```bash
azv2 --local-model create a resource group called mygroup in westus
```

### Azure Copilot Service - ![Status](https://img.shields.io/badge/status-reviewing-orange)

**Technology:** Azure Copilot infrastructure with CLI/PowerShell handlers

**Features:**
- Production Copilot infrastructure
- Built-in Azure documentation grounding
- Enterprise-grade security
- Requires Azure authentication (`azv2 login`)

**Note:** Under architectural review, seeking alternative designs.

For detailed information on each approach, configuration, and performance characteristics, see [Agent Implementation Approaches](agent-experience/agent-approaches.md).

---


## Evaluation Criteria

### Metrics

#### 1. Factual Accuracy
Correctness of generated commands against Azure CLI/PowerShell standards.

**Measurement:**
- Command syntax correctness
- Parameter accuracy (required vs. optional)
- Azure naming convention compliance

**Test Sources:**
- Azure CLI/PowerShell official documentation
- Automated validation against CLI parsers

#### 2. Latency to First Token

Time from user input to first response character.

**Targets:**
- Excellent: < 500ms
- Good: 500ms - 1500ms
- Acceptable: 1500ms - 3000ms

#### 3. Groundedness

Ability to stay focused on Azure command generation without off-topic responses.

**Measurement:**
- Task completion rate
- Proper use of three interaction patterns
- Correct response type selection

---

## Future Considerations

- **Context Awareness**: Detect current Azure subscription/tenant, infer resource group
- **Learning & Feedback**: User feedback collection, telemetry for improvement
- **Multi-Language Support**: Localization of prompts and responses
- **Advanced Features**: Multi-step command sequences, command explanation, cost estimation

---

## References

- [Azure CLI Documentation](https://learn.microsoft.com/cli/azure/)
- [Azure PowerShell Documentation](https://learn.microsoft.com/powershell/azure/)
- [Azure CLI Agent Experience](agent-experience/agent-experience-azcli.md)
- [Azure PowerShell Agent Experience](agent-experience/agent-experience-azps.md)
- [Agent Implementation Approaches](agent-experience/agent-approaches.md)
- [Architecture Overview](ARCHITECTURE.md)

