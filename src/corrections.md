- There is no need to detect the ouput from the agent engine. I know the flavour when I call it to start withl
 - ORCHESTRATION ENGINE IS NOT SHARED.. NO NEED TO KNOW ABOUT POWERSHELL var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";

 LINE 76 ORCHESTRATOR SHOULD JUST CALL THE COMMAND ENGING BACK