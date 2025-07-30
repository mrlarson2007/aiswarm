# CI/CD Pipeline Documentation

This document describes the comprehensive CI/CD pipeline implemented for the AI Swarm Agent Launcher project using GitHub Actions.

## Overview

The CI/CD pipeline consists of four main workflows that ensure code quality, security, and reliable releases:

1. **Build and Test** - Continuous integration for all code changes
2. **Release** - Automated release creation and distribution
3. **Code Quality** - Static analysis and code formatting enforcement
4. **Security** - Security scanning and vulnerability assessment

## Workflows

### 1. Build and Test (`build-and-test.yml`)

**Purpose:** Validates code changes across multiple platforms and creates distributable artifacts.

**Triggers:**
- Push to `main` or `master` branch
- Pull requests targeting `main` or `master`
- Manual workflow dispatch

**Jobs:**
- **Build Matrix:** Tests across Ubuntu, Windows, and macOS with .NET 9.0
- **Package Verification:** Validates NuGet package installation
- **Artifact Creation:** Generates self-contained executables and NuGet packages

**Outputs:**
- Self-contained executables for each platform
- NuGet package for global tool installation
- Test results (when tests are available)

### 2. Release (`release.yml`)

**Purpose:** Creates official releases with cross-platform binaries and proper versioning.

**Triggers:**
- Git tag creation matching `v*.*.*` pattern
- Manual workflow dispatch with version input

**Process:**
1. Updates project version in `.csproj` file
2. Builds release binaries for all platforms
3. Creates compressed archives with checksums
4. Generates GitHub release with release notes
5. Uploads all artifacts to the release

**Release Assets:**
- `aiswarm-{version}-win-x64.zip` - Windows executable
- `aiswarm-{version}-linux-x64.tar.gz` - Linux executable
- `aiswarm-{version}-osx-x64.tar.gz` - macOS executable
- `AiSwarm.AgentLauncher.{version}.nupkg` - NuGet package
- SHA256 checksum files for verification

### 3. Code Quality (`code-quality.yml`)

**Purpose:** Enforces code quality standards and best practices.

**Triggers:**
- Push to `main` or `master` branch
- Pull requests targeting `main` or `master`
- Manual workflow dispatch

**Checks:**
- .NET code formatting validation
- Static code analysis
- Markdown linting for documentation
- Project structure validation
- Dependency vulnerability scanning
- Quality gate enforcement

**Quality Gates:**
- Code formatting must pass
- Static analysis must complete without critical issues
- Project structure must be valid
- License compliance must be verified

### 4. Security (`security.yml`)

**Purpose:** Provides comprehensive security scanning and vulnerability assessment.

**Triggers:**
- Push to `main` or `master` branch
- Pull requests targeting `main` or `master`
- Daily scheduled runs (2 AM UTC) (implemented via `schedule` trigger in `security.yml`)
- Manual workflow dispatch

**Security Scans:**
- **CodeQL Analysis:** Advanced semantic code analysis for C#
- **Dependency Scanning:** Vulnerability assessment of NuGet packages
- **Secret Scanning:** Detection of accidentally committed secrets
- **License Compliance:** Verification of license requirements

**Security Gates:**
- CodeQL analysis must pass without critical findings
- License compliance must be maintained
- Dependency vulnerabilities are tracked and reported

## Release Process

### Automated Release

1. **Create a Tag:**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Automatic Process:**
   - Release workflow triggers automatically
   - Version is extracted from the tag
   - Cross-platform builds are created
   - GitHub release is generated with artifacts
   - Release notes are automatically generated

### Manual Release

1. **Go to Actions tab** in the GitHub repository
2. **Select "Release" workflow**
3. **Click "Run workflow"**
4. **Enter the version** (e.g., v1.0.0)
5. **Click "Run workflow"**

### Post-Release

- Verify all artifacts are attached to the release
- Test the NuGet package installation
- Update documentation if needed
- Announce the release in appropriate channels

## Installation Methods

### Global Tool (Recommended)

```bash
# From GitHub Releases
dotnet tool install --global AiSwarm.AgentLauncher

# From local package
dotnet tool install --global --add-source . AiSwarm.AgentLauncher
```

### Platform-Specific Binaries

1. Download appropriate archive from GitHub Releases
2. Extract to desired location
3. Add to PATH (optional)
4. Verify with `aiswarm --help`

## Development Guidelines

### Branch Protection

- All workflows must pass before merging to main
- Quality and security gates enforce standards
- Cross-platform builds ensure compatibility

### Version Management

- Use semantic versioning (SemVer)
- Tag format: `v{major}.{minor}.{patch}`
- Version is automatically updated during release

### Artifact Retention

- Build artifacts: 30 days
- Release artifacts: 90 days
- Security scan results: 30 days

## Troubleshooting

### Build Failures

1. **Check .NET Version:** Ensure .NET 9.0 SDK is available
2. **Dependency Issues:** Review package references and restore
3. **Platform Compatibility:** Verify code works across all target platforms

### Release Issues

1. **Tag Format:** Ensure tags follow `v*.*.*` pattern
2. **Version Conflicts:** Check for existing releases with same version
3. **Artifact Generation:** Verify all platforms build successfully

### Security Alerts

1. **CodeQL Findings:** Review and address code quality issues
2. **Dependency Vulnerabilities:** Update packages to secure versions
3. **Secret Detection:** Remove any exposed secrets immediately

## Monitoring

### Status Badges

The README includes status badges for all workflows:
- Build and Test status
- Code Quality status
- Security status
- Release status

### Notifications

- Workflow failures send notifications to repository maintainers
- Security findings are reported through GitHub Security tab
- Release notifications are sent to watchers

## Future Enhancements

### Planned Improvements

- **NuGet Publishing:** Automatic publishing to NuGet Gallery
- **Performance Testing:** Automated performance regression testing
- **Integration Testing:** End-to-end workflow testing
- **Code Coverage:** Test coverage reporting and enforcement

### Configuration Options

- **Branch Protection Rules:** Enforce quality gates for all PRs
- **Required Status Checks:** Prevent merging without passing checks
- **Auto-merge:** Enable automatic merging when all checks pass

## Contributing

When contributing to the project:

1. All workflows must pass for PRs
2. Follow existing code formatting standards
3. Add tests for new functionality
4. Update documentation for significant changes
5. Ensure security compliance

## Support

For CI/CD related issues:

1. Check workflow logs in the Actions tab
2. Review this documentation
3. Create an issue with workflow details
4. Contact maintainers for assistance