using System.Management.Automation;
using System.Text;
using Core.Agent;

namespace Agent.PowerShell;

/// <summary>
/// Cmdlet that uses AI agent to interpret natural language and execute PowerShell cmdlets
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "AzV2")]
[Alias("ask")]
public class InvokeAzV2Cmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0, ValueFromRemainingArguments = true)]
    public string[]? Query { get; set; }

    protected override void ProcessRecord()
    {
        if (Query == null || Query.Length == 0)
        {
            WriteError(new ErrorRecord(
                new ArgumentException("Please provide a query"),
                "NoQuery",
                ErrorCategory.InvalidArgument,
                null));
            return;
        }

        var userInput = string.Join(" ", Query);
        
        Task.Run(async () =>
        {
            try
            {
                // Create agent with PowerShell flavor
                var agent = await CloudAgent.CreateAsync("azure powershell");
                
                string? potentialCommand = null;
                var conversationInput = userInput;
                
                // Conversation loop
                while (true)
                {
                    // Show progress
                    var progressRecord = new ProgressRecord(0, "AI Agent", "Thinking...");
                    WriteProgress(progressRecord);
                    
                    var responseBuilder = new StringBuilder();
                    var startTime = DateTime.Now;
                    
                    // Stream the response
                    await foreach (var update in agent.RunStreamingAsync(conversationInput))
                    {
                        var text = update.ToString();
                        responseBuilder.Append(text);
                        
                        // Update progress bar
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        var percentComplete = Math.Min(95, (int)((elapsed / 20.0) * 100));
                        progressRecord.PercentComplete = percentComplete;
                        WriteProgress(progressRecord);
                    }
                    
                    // Complete progress
                    progressRecord.RecordType = ProgressRecordType.Completed;
                    WriteProgress(progressRecord);
                    
                    var fullResponse = responseBuilder.ToString();
                    
                    // Display response (excluding [DONE] marker)
                    var displayText = fullResponse.Replace("[DONE]", "").Trim();
                    if (!string.IsNullOrEmpty(displayText))
                    {
                        Host.UI.WriteLine(ConsoleColor.Cyan, Host.UI.RawUI.BackgroundColor, displayText);
                    }
                    
                    // Extract command if not already found
                    if (potentialCommand == null)
                    {
                        var lines = fullResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if ((trimmed.StartsWith("New-AzV2") || trimmed.StartsWith("Get-AzV2") ||
                                 trimmed.StartsWith("Set-AzV2") || trimmed.StartsWith("Remove-AzV2") ||
                                 trimmed.StartsWith("Connect-AzV2") || trimmed.StartsWith("Disconnect-AzV2")) &&
                                !trimmed.Contains('<') && !trimmed.Contains('>'))
                            {
                                potentialCommand = trimmed;
                                break;
                            }
                        }
                    }
                    
                    // Check if done
                    if (fullResponse.Contains("[DONE]", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if cancelled
                        var responseLower = fullResponse.ToLowerInvariant();
                        if (responseLower.Contains("cancelled") ||
                            responseLower.Contains("won't execute") ||
                            responseLower.Contains("not executing"))
                        {
                            // User cancelled, exit
                            break;
                        }
                        
                        // Execute the command if we have one
                        if (!string.IsNullOrEmpty(potentialCommand))
                        {
                            Host.UI.WriteLine(ConsoleColor.Green, Host.UI.RawUI.BackgroundColor, 
                                $"\nExecuting: {potentialCommand}");
                            
                            // Use PowerShell to invoke the command
                            var scriptBlock = ScriptBlock.Create(potentialCommand);
                            var results = InvokeCommand.InvokeScript(scriptBlock.ToString());
                            
                            // Write results to pipeline
                            foreach (var result in results)
                            {
                                WriteObject(result);
                            }
                        }
                        
                        // Done - exit the conversation loop
                        break;
                    }
                    
                    // Get next user input
                    Host.UI.WriteLine("");
                    conversationInput = Host.UI.ReadLine();
                    
                    if (string.IsNullOrWhiteSpace(conversationInput))
                    {
                        Host.UI.WriteLine("No input provided. Exiting.");
                        break;
                    }
                    
                    if (conversationInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                        conversationInput.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        Host.UI.WriteLine("Goodbye!");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "AgentError", ErrorCategory.OperationStopped, null));
            }
        }).GetAwaiter().GetResult();
    }
}
