# Script to generate 2000 files based on GetAzDatabricksWorkspace_Get.cs template

$templatePath = "c:\Users\xidi\source\cirrus\cirrus-csharp\modules\Databricks\cli\generated\commands\GetAzDatabricksWorkspace_Get.cs"
$outputDir = "c:\Users\xidi\source\cirrus\cirrus-csharp\modules\Databricks\cli\generated\commands"

# Read the template file
$templateContent = Get-Content $templatePath -Raw

# Generate 2000 files
for ($i = 1; $i -le 2000; $i++) {
    $newFileName = "GetAzDatabricksWorkspace_Get$i.cs"
    $newFilePath = Join-Path $outputDir $newFileName
    
    # Replace class name and constructor
    $newContent = $templateContent -replace "GetAzDatabricksWorkspace_Get", "GetAzDatabricksWorkspace_Get$i"
    
    # Replace the CommandInfo Name attribute
    $newContent = $newContent -replace 'Name = "workspace show"', "Name = `"workspace show$i`""
    
    # Write the new file
    Set-Content -Path $newFilePath -Value $newContent -Encoding UTF8
    
    if ($i % 100 -eq 0) {
        Write-Output "Generated $i files..."
    }
}

Write-Output "Successfully generated 2000 files!"
