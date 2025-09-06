#!/bin/bash

# AISwarm Local Tool Deployment Script
# Deploy AISwarm.Server as a local dotnet tool and configure MCP clients

set -e  # Exit on any error

# Color definitions
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
ORANGE='\033[0;33m'
NC='\033[0m' # No Color

# Parse command line arguments
CLEAN=false
SKIP_TESTS=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --clean)
            CLEAN=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--clean] [--skip-tests]"
            echo "  --clean      Perform a clean build"
            echo "  --skip-tests Skip running tests"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Get the script directory (project root)
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_PACKAGES_DIR="$PROJECT_ROOT/tools-packages"
MCP_CONFIG_PATH="$PROJECT_ROOT/.vscode/mcp.json"

echo -e "${CYAN}🚀 AISwarm Local Tool Deployment${NC}"
echo -e "${CYAN}=================================${NC}"
echo -e "${NC}Project Root: $PROJECT_ROOT${NC}"
echo ""

# Step 1: Clean build if requested
if [ "$CLEAN" = true ]; then
    echo -e "${YELLOW}🧹 Cleaning build artifacts...${NC}"
    find "$PROJECT_ROOT" -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} + 2>/dev/null || true
    echo -e "${NC}  Cleaned bin/obj directories${NC}"
fi

# Step 2: Run tests (unless skipped)
if [ "$SKIP_TESTS" = false ]; then
    echo -e "${YELLOW}🧪 Running tests...${NC}"
    if ! dotnet test --configuration Release --verbosity quiet; then
        echo -e "${RED}❌ Tests failed! Deployment aborted.${NC}"
        exit 1
    fi
    echo -e "${GREEN}✅ All tests passed!${NC}"
fi

# Step 3: Build and package the tool
echo -e "${YELLOW}📦 Building and packaging AISwarm.Server...${NC}"
mkdir -p "$TOOLS_PACKAGES_DIR"

if ! dotnet pack "src/AISwarm.Server/AISwarm.Server.csproj" -o "$TOOLS_PACKAGES_DIR" --configuration Release --force; then
    echo -e "${RED}❌ Package build failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Package built successfully!${NC}"

# Step 4: Uninstall existing tool (if present)
echo -e "${YELLOW}🔄 Managing local tool installation...${NC}"
if dotnet tool uninstall aiswarm-server 2>/dev/null; then
    echo -e "${NC}  Uninstalled existing tool${NC}"
else
    echo -e "${NC}  No existing tool to uninstall${NC}"
fi

# Determine version from csproj
VERSION=$(grep -oP '<Version>\K[^<]+' "src/AISwarm.Server/AISwarm.Server.csproj" | head -n1)
[ -z "$VERSION" ] && VERSION="1.0.0-dev"

# Step 5: Install the new tool locally
echo -e "${YELLOW}📥 Installing local tool (version ${VERSION})...${NC}"
if ! dotnet tool install aiswarm-server --local --version "$VERSION" --add-source "$TOOLS_PACKAGES_DIR"; then
    echo -e "${RED}❌ Tool installation failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Tool installed successfully!${NC}"

# Step 6: Update VS Code MCP configuration
echo -e "${YELLOW}⚙️  Updating VS Code MCP configuration...${NC}"
if [ -f "$MCP_CONFIG_PATH" ]; then
    # Create a temporary file for the updated configuration
    TEMP_CONFIG=$(mktemp)
    
    # Use jq to update the configuration, or fall back to manual editing
    if command -v jq >/dev/null 2>&1; then
        jq '.servers.aiswarm.command = "dotnet" | .servers.aiswarm.args = ["tool", "run", "aiswarm-server"]' "$MCP_CONFIG_PATH" > "$TEMP_CONFIG"
        mv "$TEMP_CONFIG" "$MCP_CONFIG_PATH"
        echo -e "${GREEN}✅ Updated .vscode/mcp.json to use local tool${NC}"
    else
        echo -e "${ORANGE}⚠️  jq not found - please manually update .vscode/mcp.json${NC}"
        echo -e "${NC}  Change command to: \"dotnet\"${NC}"
        echo -e "${NC}  Change args to: [\"tool\", \"run\", \"aiswarm-server\"]${NC}"
    fi
else
    echo -e "${ORANGE}⚠️  MCP config file not found - creating basic configuration...${NC}"
    mkdir -p "$(dirname "$MCP_CONFIG_PATH")"
    cat > "$MCP_CONFIG_PATH" << 'EOF'
{
    "servers": {
        "aiswarm": {
            "type": "stdio",
            "command": "dotnet",
            "args": ["tool", "run", "aiswarm-server"],
            "env": {
                "WorkingDirectory": "${workspaceFolder}"
            }
        }
    }
}
EOF
    echo -e "${GREEN}✅ Created .vscode/mcp.json with local tool configuration${NC}"
fi

# Step 7: Verify the installation
echo -e "${YELLOW}🔍 Verifying installation...${NC}"
if dotnet tool list --local | grep -q "aiswarm-server"; then
    echo -e "${GREEN}✅ Tool verification successful:${NC}"
    dotnet tool list --local | grep "aiswarm-server" | sed 's/^/  /'
else
    echo -e "${RED}❌ Tool verification failed!${NC}"
    exit 1
fi

# Step 8: Display completion summary
echo ""
echo -e "${GREEN}🎉 Deployment Complete!${NC}"
echo -e "${GREEN}======================${NC}"
echo ""
echo -e "${CYAN}📋 What was done:${NC}"
echo -e "${NC}  ✅ Built and packaged AISwarm.Server${NC}"
echo -e "${NC}  ✅ Installed as local dotnet tool${NC}"
echo -e "${NC}  ✅ Updated VS Code MCP configuration${NC}"
echo -e "${NC}  ✅ Ready for meta-development workflow${NC}"
echo ""
echo -e "${CYAN}🚀 Usage:${NC}"
echo -e "${NC}  VS Code: MCP will automatically use the local tool${NC}"
echo -e "${NC}  Manual:  dotnet tool run aiswarm-server${NC}"
echo -e "${NC}  Test:    Use AISwarm tools in VS Code to develop AISwarm!${NC}"
echo ""
echo -e "${YELLOW}💡 Next time you make changes, just run this script again!${NC}"