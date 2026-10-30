# Scenario 8: Recover from a failed command with AI troubleshooting

**User goal:** "I want to recover quickly when a command fails, by asking for AI help when I need it or having it start automatically."

**Successful outcome:** The user can turn on AI troubleshooting, recover from a controlled failure manually and automatically, in one-shot and interactive styles, and turn it off again, leaving no saved failures or changed settings behind.

## Prerequisites

- AzCLIPS CLI, installed as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/scenario-1-get-started-and-profiles.md).
- You must be signed in. AI troubleshooting requires sign-in.
- AI troubleshooting is off by default. Step 1 turns it on.

AI troubleshooting is available from the AzCLIPS CLI only, so this guide has no AzCLIPS PowerShell path.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash** or **AzCLIPS CLI in PowerShell**. Each path follows the same steps.

- **1. Understand a failure:** Turn on AI troubleshooting in manual mode, request a group that does not exist and inspect the error.
- **2. Recover with AI troubleshooting:** Recover from the latest failure, and optionally choose an older saved failure from the list.
- **3. Try interactive recovery (optional):** Repeat recovery in interactive style, approving the proposed plan and then each command.
- **4. Try automatic recovery (optional):** Turn on automatic mode so AI troubleshooting starts after a failure, first with a single answer and then in interactive style.
- **5. Clean up:** Turn AI troubleshooting back off, which also clears the saved failures, and reset the style.

## AzCLIPS CLI in Bash

```bash
sub="<test-subscription-id>"
missing="azclips-does-not-exist"
```

**1. Understand a failure**

Turn on AI troubleshooting in manual mode so the failure is saved, then request a group that does not exist.

```bash
az config set --key agent.recovery.trigger --value manual
az group show --subscription-id "$sub" --name "$missing"
echo "Exit code: $?"
```

**2. Recover with AI troubleshooting**

```bash
az troubleshoot recover
```

To choose an older failure instead, list the saved failures and recover from one by ID:

```bash
az troubleshoot list --output table
az troubleshoot recover --id "<failure-id>"
```

Review the explanation and suggested fix, and apply a suggestion only if it is appropriate.

**3. Try interactive recovery (optional)**

```bash
az troubleshoot recover --style interactive
```

Approve the proposed plan, then approve each command before it runs.

**4. Try automatic recovery (optional)**

In automatic mode, AI troubleshooting starts after each failure without a separate command. Turn it on and repeat the failing command. The default style returns a single answer.

```bash
az config set --key agent.recovery.trigger --value auto
az group show --subscription-id "$sub" --name "$missing"
```

Now switch to interactive style and repeat the failing command. Approve the proposed plan, then approve each command before it runs.

```bash
az config set --key agent.recovery.style --value interactive
az group show --subscription-id "$sub" --name "$missing"
```

**5. Clean up**

```bash
az config set --key agent.recovery.style --value oneshot
az config set --key agent.recovery.trigger --value off
```

Turning AI troubleshooting off also clears the saved failures. This guide creates no Azure resources.

## AzCLIPS CLI in PowerShell

```powershell
$sub = '<test-subscription-id>'
$missing = 'azclips-does-not-exist'
```

**1. Understand a failure**

Turn on AI troubleshooting in manual mode so the failure is saved, then request a group that does not exist.

```powershell
az config set --key agent.recovery.trigger --value manual
az group show --subscription-id $sub --name $missing
"Exit code: $LASTEXITCODE"
```

**2. Recover with AI troubleshooting**

```powershell
az troubleshoot recover
```

To choose an older failure instead, list the saved failures and recover from one by ID:

```powershell
az troubleshoot list --output table
az troubleshoot recover --id "<failure-id>"
```

Review the explanation and suggested fix, and apply a suggestion only if it is appropriate.

**3. Try interactive recovery (optional)**

```powershell
az troubleshoot recover --style interactive
```

Approve the proposed plan, then approve each command before it runs.

**4. Try automatic recovery (optional)**

In automatic mode, AI troubleshooting starts after each failure without a separate command. Turn it on and repeat the failing command. The default style returns a single answer.

```powershell
az config set --key agent.recovery.trigger --value auto
az group show --subscription-id $sub --name $missing
```

Now switch to interactive style and repeat the failing command. Approve the proposed plan, then approve each command before it runs.

```powershell
az config set --key agent.recovery.style --value interactive
az group show --subscription-id $sub --name $missing
```

**5. Clean up**

```powershell
az config set --key agent.recovery.style --value oneshot
az config set --key agent.recovery.trigger --value off
```

Turning AI troubleshooting off also clears the saved failures. This guide creates no Azure resources.
