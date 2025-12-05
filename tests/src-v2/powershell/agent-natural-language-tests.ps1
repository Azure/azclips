# AzV2 PowerShell Agent Natural Language Tests

$ErrorActionPreference = "Stop"
$BIN_DIR = "/workspaces/cirrus/bin"

Write-Host "=== AzV2 PowerShell Agent Natural Language Tests ===" -ForegroundColor Cyan
Write-Host ""

# Import modules
Import-Module "$BIN_DIR/Group/Cirrus.Group.psd1"
Import-Module "$BIN_DIR/Login/Cirrus.Login.psd1"

Write-Host "Testing three scenarios:" -ForegroundColor White
Write-Host "1. 'How to' questions (syntax help - should NOT execute)" -ForegroundColor White
Write-Host "2. 'Please do it' (interactive execution - requires confirmation)" -ForegroundColor White
Write-Host "3. Mistyped syntax (agent fallback)" -ForegroundColor White
Write-Host ""

# Scenario 1: "How to" questions - Syntax help only
Write-Host "=== Scenario 1: Syntax Help (How to...) ===" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 1.1: How do I create a resource group?" -ForegroundColor Cyan
Write-Host "Expected: Syntax example with placeholders, NO [DONE] marker" -ForegroundColor Gray
Write-Host "Command: ask how do I create a resource group" -ForegroundColor White
Write-Host ""
Write-Host "NOTE: This is a manual test. Run: ask how do I create a resource group" -ForegroundColor Yellow
Write-Host "Verify: Output shows New-AzV2ResourceGroup with <placeholders> and no execution" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 1.2: How do I list all VMs?" -ForegroundColor Cyan
Write-Host "Expected: Syntax example, NO execution" -ForegroundColor Gray
Write-Host "Command: please show me how to list all virtual machines" -ForegroundColor White
Write-Host ""

Write-Host "Test 1.3: What's the syntax for login?" -ForegroundColor Cyan
Write-Host "Expected: Connect-AzV2Account syntax, NO execution" -ForegroundColor Gray
Write-Host "Command: pls what is the syntax to login to azure" -ForegroundColor White
Write-Host ""

# Scenario 2: "Please do it" - Interactive execution
Write-Host "=== Scenario 2: Interactive Execution (Please do it...) ===" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 2.1: Create a resource group (interactive)" -ForegroundColor Cyan
Write-Host "Expected: Agent asks questions, builds command, asks for confirmation, shows [DONE] marker" -ForegroundColor Gray
Write-Host "Command: ask create a resource group for testing" -ForegroundColor White
Write-Host ""
Write-Host "NOTE: This is an INTERACTIVE test. Run manually:" -ForegroundColor Yellow
Write-Host "  ask create a resource group for testing" -ForegroundColor Yellow
Write-Host "  - Agent will ask for name, location, etc." -ForegroundColor Yellow
Write-Host "  - Provide: name=agent-test-rg, location=westus" -ForegroundColor Yellow
Write-Host "  - Confirm 'yes' when asked" -ForegroundColor Yellow
Write-Host "  - Verify: Command executes and resource group is created" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 2.2: List resource groups (immediate)" -ForegroundColor Cyan
Write-Host "Expected: Agent provides command, user confirms, executes with [DONE]" -ForegroundColor Gray
Write-Host "Command: please list all my resource groups" -ForegroundColor White
Write-Host ""
Write-Host "NOTE: Run manually and confirm 'yes' when prompted" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 2.3: Login to Azure (interactive)" -ForegroundColor Cyan
Write-Host "Expected: Agent asks for details or uses defaults, confirms, executes" -ForegroundColor Gray
Write-Host "Command: pls login to azure" -ForegroundColor White
Write-Host ""

# Scenario 3: Mistyped syntax - Agent fallback
Write-Host "=== Scenario 3: Mistyped Syntax (Agent Fallback) ===" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 3.1: Mistyped cmdlet name" -ForegroundColor Cyan
Write-Host "Expected: Agent recognizes intent and provides correct syntax or executes" -ForegroundColor Gray
Write-Host "Command: Get-AzV2ResourseGroup (misspelled)" -ForegroundColor White
Write-Host ""
Write-Host "NOTE: Run manually in PowerShell:" -ForegroundColor Yellow
Write-Host "  Get-AzV2ResourseGroup" -ForegroundColor Yellow
Write-Host "  - Should fail with 'command not found'" -ForegroundColor Yellow
Write-Host "  - Then run: ask show resource groups" -ForegroundColor Yellow
Write-Host "  - Agent should understand and provide correct command" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 3.2: Wrong parameter syntax" -ForegroundColor Cyan
Write-Host "Expected: Agent corrects and provides proper syntax" -ForegroundColor Gray
Write-Host "Command: ask New-AzV2ResourceGroup -ResourceGroupName test -Loc eastus" -ForegroundColor White
Write-Host "         (wrong parameters -ResourceGroupName instead of -Name, -Loc instead of -Location)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Test 3.3: Natural language instead of cmdlet" -ForegroundColor Cyan
Write-Host "Expected: Agent understands and generates correct cmdlet" -ForegroundColor Gray
Write-Host "Command: please make a new resource group called demo-rg in west us" -ForegroundColor White
Write-Host ""

Write-Host "=== Manual Testing Instructions ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run these tests manually:" -ForegroundColor White
Write-Host "1. Import the modules if not already loaded:" -ForegroundColor White
Write-Host "   Import-Module $BIN_DIR/Group/Cirrus.Group.psd1" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Test each scenario above using the 'ask', 'please', or 'pls' aliases" -ForegroundColor White
Write-Host ""
Write-Host "3. Verify behaviors:" -ForegroundColor White
Write-Host "   - 'How to' questions: Returns syntax examples, NO [DONE] marker" -ForegroundColor Gray
Write-Host "   - 'Do it' requests: Interactive prompts, [DONE] marker, execution" -ForegroundColor Gray
Write-Host "   - Mistyped syntax: Agent fallback corrects and helps" -ForegroundColor Gray
Write-Host ""

Write-Host "=== Agent Natural Language Tests Documentation Complete ===" -ForegroundColor Cyan
Write-Host "Run the commands above manually to verify agent behavior" -ForegroundColor Yellow
