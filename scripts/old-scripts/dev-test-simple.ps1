# Test script for SwAIvyn development environment (without emojis)
param (
    [string] $StackName = 'swaivyn',
    [int] $TraefikPort = 80,
    [switch] $SkipServiceTests = $false
)

$ErrorActionPreference = "Continue"
$rootDir = Split-Path -Parent $PSScriptRoot
$testResults = @{}
$overallSuccess = $true

function Record-Test {
    param([string]$TestName, [bool]$Success, [string]$Details = "")
    $testResults[$TestName] = @{
        Success = $Success
        Details = $Details
        Timestamp = Get-Date
    }
    if (-not $Success) {
        $script:overallSuccess = $false
    }
    
    $status = if ($Success) { "[PASS]" } else { "[FAIL]" }
    $color = if ($Success) { "Green" } else { "Red" }
    Write-Host "$status - $TestName" -ForegroundColor $color
    if ($Details) {
        Write-Host "    $Details" -ForegroundColor DarkGray
    }
}

function Test-Command { 
    param([string]$Name) 
    try { 
        Get-Command $Name -ErrorAction Stop | Out-Null
        return $true 
    } catch { 
        return $false 
    } 
}

function Test-TcpPort {
    param([string]$HostName = 'localhost', [int]$Port, [int]$TimeoutSec = 5)
    try {
        $result = Test-NetConnection -ComputerName $HostName -Port $Port -WarningAction SilentlyContinue -InformationLevel Quiet
        return $result
    } catch {
        return $false
    }
}

function Test-HttpEndpoint {
    param([string]$Url, [int]$TimeoutSec = 10)
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 300
    } catch {
        return $false
    }
}

Write-Host "SwAIvyn Development Environment Test Suite" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

# Test 1: Prerequisites
Write-Host "`nTesting Prerequisites..." -ForegroundColor Yellow

$dockerExists = Test-Command 'docker'
Record-Test "Docker CLI Available" $dockerExists

if ($dockerExists) {
    try {
        $dockerRunning = (& docker info 2>$null) -ne $null
        Record-Test "Docker Daemon Running" $dockerRunning
        
        if ($dockerRunning) {
            $swarmState = (& docker info --format '{{.Swarm.LocalNodeState}}' 2>$null).Trim()
            $swarmActive = $swarmState -eq 'active'
            Record-Test "Docker Swarm Active" $swarmActive $swarmState
        }
    } catch {
        Record-Test "Docker Daemon Running" $false $_.Exception.Message
    }
}

$nodeExists = Test-Command 'node'
Record-Test "Node.js Available" $nodeExists

$npmExists = Test-Command 'npm'
Record-Test "npm Available" $npmExists

$pythonExists = Test-Command 'py'
if (-not $pythonExists) {
    $pythonExists = Test-Command 'python'
}
Record-Test "Python Available" $pythonExists

# Test 2: File Structure
Write-Host "`nTesting File Structure..." -ForegroundColor Yellow

$requiredFiles = @(
    'docker-stack.yml',
    'scripts/dev-run.ps1',
    'scripts/dev-bff.ps1', 
    'scripts/dev-frontend.ps1',
    'scripts/dev-orchestrator.ps1',
    'Services/bff',
    'frontend'
)

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $rootDir $file
    $exists = Test-Path $fullPath
    Record-Test "File/Dir Exists: $file" $exists $fullPath
}

# Test 3: Docker Stack Services
Write-Host "`nTesting Docker Stack Services..." -ForegroundColor Yellow

if ($dockerExists) {
    try {
        $services = & docker service ls --filter "label=com.docker.stack.namespace=$StackName" --format "{{.Name}}" 2>$null
        $expectedServices = @(
            "${StackName}_traefik",
            "${StackName}_temporal", 
            "${StackName}_swai-db",
            "${StackName}_qdrant",
            "${StackName}_neo4j"
        )
        
        foreach ($expectedService in $expectedServices) {
            $running = $services -contains $expectedService
            Record-Test "Service Running: $expectedService" $running
        }
        
        if ($services) {
            $serviceDetails = & docker service ls --filter "label=com.docker.stack.namespace=$StackName" --format "table {{.Name}}\t{{.Replicas}}" 2>$null
            Record-Test "Docker Services Status" $true $serviceDetails
        }
        
    } catch {
        Record-Test "Docker Services Check" $false $_.Exception.Message
    }
}

# Test 4: Network Connectivity
Write-Host "`nTesting Network Connectivity..." -ForegroundColor Yellow

$criticalPorts = @{
    'Traefik HTTP' = $TraefikPort
    'PostgreSQL' = 5432
    'Temporal' = 7233
    'Neo4j Bolt' = 7687
    'Qdrant' = 6333
}

foreach ($service in $criticalPorts.Keys) {
    $port = $criticalPorts[$service]
    $connected = Test-TcpPort -HostName 'localhost' -Port $port -TimeoutSec 5
    Record-Test "Port Connectivity: $service (:$port)" $connected
}

# Test 5: HTTP Health Checks
Write-Host "`nTesting HTTP Health Endpoints..." -ForegroundColor Yellow

$healthEndpoints = @{
    'Traefik Dashboard' = "http://traefik.localhost:$TraefikPort"
    'Qdrant API' = "http://qdrant.localhost:$TraefikPort"
    'Neo4j Browser' = "http://graph.localhost:$TraefikPort"
}

foreach ($endpoint in $healthEndpoints.Keys) {
    $url = $healthEndpoints[$endpoint]
    $healthy = Test-HttpEndpoint -Url $url -TimeoutSec 10
    Record-Test "HTTP Endpoint: $endpoint" $healthy $url
}

# Test 6: Individual Service Scripts
Write-Host "`nTesting Individual Service Scripts..." -ForegroundColor Yellow

if (-not $SkipServiceTests) {
    $serviceScripts = @{
        'dev-bff.ps1' = 'scripts/dev-bff.ps1'
        'dev-frontend.ps1' = 'scripts/dev-frontend.ps1'
        'dev-orchestrator.ps1' = 'scripts/dev-orchestrator.ps1'
    }
    
    foreach ($script in $serviceScripts.Keys) {
        $scriptPath = Join-Path $rootDir $serviceScripts[$script]
        
        if (Test-Path $scriptPath) {
            try {
                $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content $scriptPath -Raw), [ref]$null)
                Record-Test "Script Syntax: $script" $true
            } catch {
                Record-Test "Script Syntax: $script" $false $_.Exception.Message
            }
        } else {
            Record-Test "Script Exists: $script" $false "File not found: $scriptPath"
        }
    }
}

# Test 7: Environment Variables
Write-Host "`nTesting Environment Configuration..." -ForegroundColor Yellow

$envFile = Join-Path $rootDir '.env'
$envExists = Test-Path $envFile
Record-Test ".env File Exists" $envExists $envFile

if ($envExists) {
    try {
        $envContent = Get-Content $envFile -Raw
        $hasPostgresPassword = $envContent -match 'POSTGRES_PASSWORD\s*='
        $hasNeo4jPassword = $envContent -match 'NEO4J_PASSWORD\s*='
        
        Record-Test ".env Has POSTGRES_PASSWORD" $hasPostgresPassword
        Record-Test ".env Has NEO4J_PASSWORD" $hasNeo4jPassword
    } catch {
        Record-Test ".env File Readable" $false $_.Exception.Message
    }
}

# Test 8: Python Virtual Environments
Write-Host "`nTesting Python Virtual Environments..." -ForegroundColor Yellow

$pythonServices = @('Services/bff', 'Services/orchestrator')
foreach ($serviceDir in $pythonServices) {
    $fullPath = Join-Path $rootDir $serviceDir
    if (Test-Path $fullPath) {
        $venvPath = Join-Path $fullPath '.venv'
        $venvExists = Test-Path $venvPath
        Record-Test "Python venv: $serviceDir" $venvExists $venvPath
        
        if ($venvExists) {
            $pythonExe = Join-Path $venvPath 'Scripts/python.exe'
            $pythonExeExists = Test-Path $pythonExe
            Record-Test "Python executable: $serviceDir" $pythonExeExists $pythonExe
        }
    }
}

# Test 9: Frontend Dependencies
Write-Host "`nTesting Frontend Dependencies..." -ForegroundColor Yellow

$frontendDir = Join-Path $rootDir 'frontend'
if (Test-Path $frontendDir) {
    $nodeModules = Join-Path $frontendDir 'node_modules'
    $nodeModulesExists = Test-Path $nodeModules
    Record-Test "Frontend node_modules" $nodeModulesExists $nodeModules
    
    $packageJson = Join-Path $frontendDir 'package.json'
    $packageJsonExists = Test-Path $packageJson
    Record-Test "Frontend package.json" $packageJsonExists $packageJson
}

# Test 10: Docker Images
Write-Host "`nTesting Docker Images..." -ForegroundColor Yellow

if ($dockerExists) {
    $expectedImages = @(
        'swai/tts-proxy:local',
        'swai/tts-11labs-adapter:local'
    )
    
    foreach ($image in $expectedImages) {
        try {
            $imageExists = (& docker images -q $image 2>$null) -ne ""
            Record-Test "Docker Image: $image" $imageExists
        } catch {
            Record-Test "Docker Image: $image" $false $_.Exception.Message
        }
    }
}

# Generate Test Report
Write-Host "`nTest Results Summary" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

$totalTests = $testResults.Count
$passedTests = ($testResults.Values | Where-Object { $_.Success }).Count
$failedTests = $totalTests - $passedTests
$successRate = [math]::Round(($passedTests / $totalTests) * 100, 1)

Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $passedTests" -ForegroundColor Green  
Write-Host "Failed: $failedTests" -ForegroundColor Red
Write-Host "Success Rate: $successRate%" -ForegroundColor $(if($successRate -ge 80) { "Green" } else { "Yellow" })

if ($failedTests -gt 0) {
    Write-Host "`nFailed Tests:" -ForegroundColor Red
    foreach ($test in $testResults.Keys) {
        $result = $testResults[$test]
        if (-not $result.Success) {
            Write-Host "  * $test" -ForegroundColor Red
            if ($result.Details) {
                Write-Host "    $($result.Details)" -ForegroundColor DarkGray
            }
        }
    }
}

Write-Host "`nOverall Result: " -NoNewline
if ($overallSuccess -and $successRate -ge 90) {
    Write-Host "EXCELLENT [PASS]" -ForegroundColor Green
    Write-Host "Your development environment is ready to go!" -ForegroundColor Green
} elseif ($successRate -ge 70) {
    Write-Host "GOOD [PARTIAL]" -ForegroundColor Yellow
    Write-Host "Most components are working. Check failed tests above." -ForegroundColor Yellow
} else {
    Write-Host "NEEDS WORK [FAIL]" -ForegroundColor Red
    Write-Host "Several issues need to be resolved before the environment is ready." -ForegroundColor Red
}

Write-Host "`nNext Steps:" -ForegroundColor Cyan
if ($failedTests -gt 0) {
    Write-Host "1. Address the failed tests listed above" -ForegroundColor White
    Write-Host "2. Run the test again to verify fixes" -ForegroundColor White
    Write-Host "3. If all tests pass, try running: .\SwAIvyn\scripts\dev-run.ps1" -ForegroundColor White
} else {
    Write-Host "All tests passed! You can now run:" -ForegroundColor White
    Write-Host "  .\SwAIvyn\scripts\dev-run.ps1" -ForegroundColor Green
}

# Export test results to file
$testResultsPath = Join-Path $PSScriptRoot "dev-test-results.json"
$testResults | ConvertTo-Json -Depth 3 | Set-Content -Path $testResultsPath -Encoding UTF8
Write-Host "`nDetailed test results saved to: $testResultsPath" -ForegroundColor DarkGray

return $overallSuccess
