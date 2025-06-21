# 内存泄漏测试脚本
# 用于验证修复后的内存管理是否正常工作

param(
    [int]$Iterations = 1000,
    [switch]$Verbose
)

Write-Host "=== Cirrus Memory Leak Test ===" -ForegroundColor Cyan

# 导入模块
try {
    Import-Module "$PSScriptRoot\Cirrus.psd1" -Force
    Write-Host "✓ Cirrus module loaded" -ForegroundColor Green
}
catch {
    Write-Error "Failed to load Cirrus module: $_"
    exit 1
}

# 获取初始内存使用情况
$initialMemory = [System.GC]::GetTotalMemory($false) / 1MB
Write-Host "Initial memory usage: $($initialMemory.ToString('F2')) MB" -ForegroundColor Gray

# 测试1: 简单参数测试
Write-Host "`n--- Test 1: Simple Parameters ---" -ForegroundColor Yellow
for ($i = 1; $i -le ($Iterations / 10); $i++) {
    try {
        $result = Invoke-CirrusCommand -Uri "test://simple" -Parameters @{
            name    = "test$i"
            count   = $i
            enabled = ($i % 2 -eq 0)
        }
        
        if ($Verbose -and ($i % 100 -eq 0)) {
            $currentMemory = [System.GC]::GetTotalMemory($false) / 1MB
            Write-Host "  Iteration $i - Memory: $($currentMemory.ToString('F2')) MB" -ForegroundColor Gray
        }
    }
    catch {
        Write-Warning "Test 1 failed at iteration $i : $_"
    }
}
Write-Host "✓ Simple parameters test completed" -ForegroundColor Green

# 测试2: 复杂对象测试
Write-Host "`n--- Test 2: Complex Objects ---" -ForegroundColor Yellow
for ($i = 1; $i -le ($Iterations / 10); $i++) {
    try {
        $complexObject = @{
            vm      = @{
                name     = "vm$i"
                size     = "Standard_B2s"
                location = "eastus"
                tags     = @("test", "iteration$i")
                disks    = @(
                    @{ name = "disk1"; size = 128 },
                    @{ name = "disk2"; size = 256 }
                )
            }
            network = @{
                vnet   = "vnet$i"
                subnet = "subnet$i"
                nsg    = @{
                    rules = @(
                        @{ port = 80; protocol = "TCP" },
                        @{ port = 443; protocol = "TCP" }
                    )
                }
            }
        }
        
        $result = Invoke-CirrusCommand -Uri "test://complex" -Parameters @{
            config    = $complexObject
            timestamp = (Get-Date).ToString()
        }
        
        if ($Verbose -and ($i % 50 -eq 0)) {
            $currentMemory = [System.GC]::GetTotalMemory($false) / 1MB
            Write-Host "  Iteration $i - Memory: $($currentMemory.ToString('F2')) MB" -ForegroundColor Gray
        }
    }
    catch {
        Write-Warning "Test 2 failed at iteration $i : $_"
    }
}
Write-Host "✓ Complex objects test completed" -ForegroundColor Green

# 测试3: 大数组测试
Write-Host "`n--- Test 3: Large Arrays ---" -ForegroundColor Yellow
for ($i = 1; $i -le ($Iterations / 20); $i++) {
    try {
        $largeArray = @()
        for ($j = 1; $j -le 50; $j++) {
            $largeArray += @{
                id    = "$i-$j"
                value = "value$j"
                data  = (1..$j) -join ","
            }
        }
        
        $result = Invoke-CirrusCommand -Uri "test://array" -Parameters @{
            items = $largeArray
            batch = $i
        }
        
        if ($Verbose -and ($i % 25 -eq 0)) {
            $currentMemory = [System.GC]::GetTotalMemory($false) / 1MB
            Write-Host "  Iteration $i - Memory: $($currentMemory.ToString('F2')) MB" -ForegroundColor Gray
        }
    }
    catch {
        Write-Warning "Test 3 failed at iteration $i : $_"
    }
}
Write-Host "✓ Large arrays test completed" -ForegroundColor Green

# 强制垃圾回收
Write-Host "`n--- Forcing Garbage Collection ---" -ForegroundColor Yellow
[System.GC]::Collect()
[System.GC]::WaitForPendingFinalizers()
[System.GC]::Collect()
Start-Sleep -Seconds 2

# 获取最终内存使用情况
$finalMemory = [System.GC]::GetTotalMemory($false) / 1MB
$memoryDiff = $finalMemory - $initialMemory

Write-Host "`n=== Memory Usage Summary ===" -ForegroundColor Cyan
Write-Host "Initial memory: $($initialMemory.ToString('F2')) MB" -ForegroundColor White
Write-Host "Final memory:   $($finalMemory.ToString('F2')) MB" -ForegroundColor White
Write-Host "Difference:     $($memoryDiff.ToString('F2')) MB" -ForegroundColor $(if ($memoryDiff -lt 10) { "Green" } elseif ($memoryDiff -lt 50) { "Yellow" } else { "Red" })

if ($memoryDiff -lt 10) {
    Write-Host "✓ Memory usage appears normal" -ForegroundColor Green
}
elseif ($memoryDiff -lt 50) {
    Write-Host "⚠ Memory usage slightly elevated - monitor for leaks" -ForegroundColor Yellow
}
else {
    Write-Host "❌ Possible memory leak detected!" -ForegroundColor Red
}

# 测试版本信息
Write-Host "`n--- Version Information ---" -ForegroundColor Yellow
try {
    $version = Get-CirrusVersion
    Write-Host "Module Version: $($version.PSModuleVersion)" -ForegroundColor White
    Write-Host "Rust Library Version: $($version.RustLibraryVersion)" -ForegroundColor White
    Write-Host "Initialized: $($version.IsInitialized)" -ForegroundColor White
}
catch {
    Write-Warning "Failed to get version information: $_"
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan 