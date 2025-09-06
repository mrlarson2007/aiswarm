#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Deploy AISwarm.Server as a local dotnet tool and configure MCP clients to use it.

.DESCRIPTION
    This script automates the complete local deployment process:
    1. Builds and packages AISwarm.Server as a dotnet tool
    2. Installs/updates the tool locally
    3. Updates VS Code MCP configuration to use the local tool
    4. Ensures the tool is ready for use in VS Code and Gemini CLI

.PARAMETER Clean
    Perform a clean build (remove bin/obj directories first)

.PARAMETER SkipTests
    Skip running tests before deployment

.EXAMPLE
    .\deploy-local-tool.ps1
    Deploy with default settings

.EXAMPLE
    .\deploy-local-tool.ps1 -Clean
    Clean build and deploy

.EXAMPLE
    .\deploy-local-tool.ps1 -SkipTests
    Deploy without running tests
#>

param(
    [switch]$Clean,
    [switch]$SkipTests
)

# Set error handling
$ErrorActionPreference = "Stop"

# Get the script directory (project root)
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ToolsPackagesDir = Join-Path $ProjectRoot "tools-packages"
$McpConfigPath = Join-Path $ProjectRoot ".vscode\mcp.json"
$AgentLauncherCsprojPath = Join-Path $ProjectRoot "src\AgentLauncher\AgentLauncher.csproj"
$McpServerCsprojPath = Join-Path $ProjectRoot "src\AISwarm.Server\AISwarm.Server.csproj"

Write-Host "🚀 AISwarm Local Tool Deployment" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Gray
Write-Host ""

# Step 0: Auto-increment version
Write-Host "🔢 Auto-incrementing version..." -ForegroundColor Yellow
if (Test-Path $McpServerCsprojPath) {
    $csprojContent = Get-Content $McpServerCsprojPath -Raw
    $versionMatch = [regex]::Match($csprojContent, '<Version>([^<]+)</Version>')
    
    if ($versionMatch.Success) {
        $currentVersion = $versionMatch.Groups[1].Value
        
        # Parse version (handle both x.y.z and x.y.z-dev formats)
        $versionParts = $currentVersion -split '-'
        $baseVersion = $versionParts[0]
        $suffix = if ($versionParts.Length -gt 1) { "-" + $versionParts[1] } else { "" }
        
        $parts = $baseVersion -split '\.'
        if ($parts.Length -eq 3) {
            $major = [int]$parts[0]
            $minor = [int]$parts[1]
            $patch = [int]$parts[2]
            
            # Increment patch version
            $newPatch = $patch + 1
            $newBaseVersion = "$major.$minor.$newPatch"
            $newVersion = "$newBaseVersion$suffix"
            
            # Update the csproj file
            $newCsprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$newVersion</Version>"
            Set-Content -Path $McpServerCsprojPath -Value $newCsprojContent -Encoding UTF8
            
            Write-Host "  Version incremented: $currentVersion → $newVersion" -ForegroundColor Gray
            $toolVersion = $newVersion
        } else {
            Write-Host "⚠️  Could not parse version format, using current: $currentVersion" -ForegroundColor DarkYellow
            $toolVersion = $currentVersion
        }
    } else {
    Write-Host "⚠️  Could not find version in csproj, using default" -ForegroundColor DarkYellow
        $toolVersion = "1.0.0-dev"
    }
} else {
    Write-Host "⚠️  AISwarm.Server.csproj not found, using default version" -ForegroundColor DarkYellow
    $toolVersion = "1.0.0-dev"
}

# Step 1: Clean build if requested
if ($Clean) {
    Write-Host "🧹 Cleaning build artifacts..." -ForegroundColor Yellow
    Get-ChildItem -Path $ProjectRoot -Recurse -Directory | Where-Object { $_.Name -in @('bin','obj') } | ForEach-Object {
        $fullPath = $_.FullName
        try {
            Remove-Item $fullPath -Recurse -Force -ErrorAction Stop
            Write-Host "  Removed: $fullPath" -ForegroundColor Gray
        } catch {
            Write-Host "  ⚠️  Could not remove: $fullPath ($_ )" -ForegroundColor DarkYellow
        }
    }
}

# Step 2: Run tests (unless skipped)
if (-not $SkipTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    $testResult = dotnet test --configuration Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Tests failed! Deployment aborted." -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ All tests passed!" -ForegroundColor Green
}

# Step 3: Build and package the tool
Write-Host "�️  Building AISwarm.Server (Release)..." -ForegroundColor Yellow
dotnet build $McpServerCsprojPath -c Release -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Packing AISwarm.Server as dotnet tool..." -ForegroundColor Yellow
if (-not (Test-Path $ToolsPackagesDir)) {
    New-Item -ItemType Directory -Path $ToolsPackagesDir -Force | Out-Null
}

$packResult = dotnet pack $McpServerCsprojPath -o $ToolsPackagesDir --configuration Release --force -v:normal 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Package build failed!" -ForegroundColor Red
    Write-Host "--- dotnet pack output ---" -ForegroundColor DarkYellow
    Write-Host $packResult
    Write-Host "--------------------------" -ForegroundColor DarkYellow
    exit 1
}
Write-Host "✅ Package built successfully!" -ForegroundColor Green

# Step 4: Uninstall existing tool (if present)
Write-Host "🔄 Managing local tool installation..." -ForegroundColor Yellow
$uninstallResult = dotnet tool uninstall aiswarm-server 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Uninstalled existing tool" -ForegroundColor Gray
} else {
    Write-Host "  No existing tool to uninstall" -ForegroundColor Gray
}

# Step 5: Install the new tool locally
Write-Host "📥 Installing local tool..." -ForegroundColor Yellow
$installResult = dotnet tool install aiswarm-server --local --version $toolVersion --add-source $ToolsPackagesDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Tool installation failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Tool installed successfully!" -ForegroundColor Green

# Step 6: Update VS Code MCP configuration
Write-Host "⚙️  Updating VS Code MCP configuration..." -ForegroundColor Yellow
if (Test-Path $McpConfigPath) {
    # Read current configuration
    $mcpConfig = Get-Content $McpConfigPath -Raw | ConvertFrom-Json
    
    # Update the aiswarm server configuration to use the local tool
    if ($mcpConfig.servers.aiswarm) {
        $mcpConfig.servers.aiswarm.command = "dotnet"
        $mcpConfig.servers.aiswarm.args = @("tool", "run", "aiswarm-server")
        
        # Convert back to JSON with proper formatting
        $jsonOutput = $mcpConfig | ConvertTo-Json -Depth 10
        
        # Write back to file
        Set-Content -Path $McpConfigPath -Value $jsonOutput -Encoding UTF8
        Write-Host "✅ Updated .vscode\mcp.json to use local tool" -ForegroundColor Green
    } else {
        Write-Host "⚠️  No 'aiswarm' server found in MCP config - you may need to add it manually" -ForegroundColor DarkYellow
    }
} else {
    Write-Host "⚠️  MCP config file not found - creating basic configuration..." -ForegroundColor DarkYellow
    $newMcpConfig = @{
        servers = @{
            aiswarm = @{
                type = "stdio"
                command = "dotnet"
                args = @("tool", "run", "aiswarm-server")
                env = @{
                    WorkingDirectory = "`${workspaceFolder}"
                }
            }
        }
    }
    
    $jsonOutput = $newMcpConfig | ConvertTo-Json -Depth 10
    $vsCodeDir = Split-Path $McpConfigPath -Parent
    if (-not (Test-Path $vsCodeDir)) {
        New-Item -ItemType Directory -Path $vsCodeDir -Force | Out-Null
    }
    Set-Content -Path $McpConfigPath -Value $jsonOutput -Encoding UTF8
    Write-Host "✅ Created .vscode\mcp.json with local tool configuration" -ForegroundColor Green
}

# Step 7: Verify the installation
Write-Host "🔍 Verifying installation..." -ForegroundColor Yellow
$verifyResult = dotnet tool list --local | Select-String "aiswarm-server"
if ($verifyResult) {
    Write-Host "✅ Tool verification successful:" -ForegroundColor Green
    Write-Host "  $($verifyResult.Line)" -ForegroundColor Gray
} else {
    Write-Host "❌ Tool verification failed!" -ForegroundColor Red
    exit 1
}

# Step 8: Display completion summary
Write-Host ""
Write-Host "🎉 Deployment Complete!" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host ""
Write-Host "📋 What was done:" -ForegroundColor Cyan
Write-Host "  ✅ Built and packaged AISwarm.Server" -ForegroundColor White
Write-Host "  ✅ Installed as local dotnet tool" -ForegroundColor White
Write-Host "  ✅ Updated VS Code MCP configuration" -ForegroundColor White
Write-Host "  ✅ Ready for meta-development workflow" -ForegroundColor White
Write-Host ""
Write-Host "🚀 Usage:" -ForegroundColor Cyan
Write-Host "  VS Code: MCP will automatically use the local tool" -ForegroundColor White
Write-Host "  Manual:  dotnet tool run aiswarm-server" -ForegroundColor White
Write-Host "  Test:    Use AISwarm tools in VS Code to develop AISwarm!" -ForegroundColor White
Write-Host ""
Write-Host "💡 Next time you make changes, just run this script again!" -ForegroundColor Yellow