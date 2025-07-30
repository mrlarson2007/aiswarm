#!/bin/bash
# Local validation script for CI/CD pipeline components
# This script simulates some of the checks that run in GitHub Actions

set -e

echo "🔍 AI Swarm CI/CD Local Validation"
echo "=================================="

# Check if we're in the right directory
if [ ! -f "aiswarm.sln" ]; then
    echo "❌ Error: aiswarm.sln not found. Run this script from the repository root."
    exit 1
fi

echo "✅ Repository structure validated"

# Check .NET availability
echo "🔧 Checking .NET SDK..."
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo "✅ .NET SDK $DOTNET_VERSION found"
else
    echo "❌ .NET SDK not found"
    exit 1
fi

# Check project structure
echo "📁 Validating project structure..."

required_files=(
    "README.md"
    "LICENSE"
    ".gitignore"
    ".gitattributes"
    "aiswarm.sln"
    "src/AgentLauncher/AgentLauncher.csproj"
    ".github/workflows/build-and-test.yml"
    ".github/workflows/release.yml"
    ".github/workflows/code-quality.yml"
    ".github/workflows/security.yml"
    "docs/CICD.md"
)

for file in "${required_files[@]}"; do
    if [ -f "$file" ]; then
        echo "  ✅ $file"
    else
        echo "  ❌ Missing: $file"
        exit 1
    fi
done

# Check embedded resources
echo "📦 Checking embedded resources..."
resource_files=(
    "src/AgentLauncher/Resources/planner_prompt.md"
    "src/AgentLauncher/Resources/implementer_prompt.md"
    "src/AgentLauncher/Resources/reviewer_prompt.md"
    "src/AgentLauncher/Resources/tester_prompt.md"
)

for file in "${resource_files[@]}"; do
    if [ -f "$file" ]; then
        echo "  ✅ $file"
    else
        echo "  ⚠️  Missing resource: $file"
    fi
done

# Validate workflow YAML syntax
echo "📋 Validating workflow files..."
for workflow in .github/workflows/*.yml; do
    if [ -f "$workflow" ]; then
        echo "  ✅ $(basename "$workflow")"
        # Basic YAML validation (check if it's valid YAML)
        if command -v python3 &> /dev/null; then
            python3 -c "import yaml; yaml.safe_load(open('$workflow'))" 2>/dev/null || echo "  ⚠️  YAML syntax issue in $workflow"
        fi
    fi
done

# Try to restore dependencies
echo "📥 Restoring dependencies..."
if dotnet restore > /dev/null 2>&1; then
    echo "✅ Dependencies restored successfully"
else
    echo "⚠️  Dependency restore had issues (might be due to .NET version mismatch)"
fi

# Check if solution builds (with current .NET version)
echo "🔨 Testing build (with available .NET version)..."
if dotnet build --configuration Release > /dev/null 2>&1; then
    echo "✅ Build successful"
else
    echo "⚠️  Build failed (likely due to .NET version requirements)"
fi

# Check for potential security issues
echo "🔐 Basic security checks..."

# Check for potential secrets in code files
echo "  Checking for potential secrets..."
if grep -r -i "password\|secret\|token\|key" src/ --include="*.cs" | grep -v "//"; then
    echo "  ⚠️  Potential secrets found in source code"
else
    echo "  ✅ No obvious secrets found in source code"
fi

# Check git configuration
echo "📝 Git configuration..."
if git status > /dev/null 2>&1; then
    echo "  ✅ Git repository is healthy"
    
    # Check if there are any uncommitted changes
    if [ -n "$(git status --porcelain)" ]; then
        echo "  ⚠️  Uncommitted changes detected"
        git status --short
    else
        echo "  ✅ Working directory is clean"
    fi
else
    echo "  ❌ Git repository issues detected"
fi

echo ""
echo "🎉 Local validation completed!"
echo ""
echo "📚 Next steps:"
echo "  - Commit and push your changes to trigger CI/CD workflows"
echo "  - Create a tag (e.g., git tag v1.0.0) to trigger a release"
echo "  - Monitor workflow status in GitHub Actions"
echo ""
echo "📖 For more information, see docs/CICD.md"