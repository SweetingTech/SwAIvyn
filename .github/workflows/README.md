# GitHub Actions Workflows

This directory contains GitHub Actions workflows for automating the SwAIvyn build, test, and deployment processes.

## Workflows

###  CI (`ci.yml`)
**Triggers:** Push to main/develop, Pull Requests
- **Lint and Test Job**: Runs ESLint on frontend, builds both frontend and backend
- **Build Job**: Creates production builds for both win-x64 and win-arm64 architectures
- **Artifacts**: Uploads build artifacts for each platform

###  Release (`release.yml`)
**Triggers:** Git tags (v*), Manual dispatch
- Builds release packages for both architectures
- Creates zip packages with all necessary files
- Automatically creates GitHub releases with assets
- Supports manual version specification

### [OK] PR Validation (`pr-validation.yml`)
**Triggers:** Pull Request events
- Fast validation of PRs without full builds
- Checks for common issues and syntax errors
- Posts validation results as PR comments
- Includes concurrency control to cancel outdated runs

###  Dependencies (`dependencies.yml`)
**Triggers:** Weekly schedule, Manual dispatch
- **Dependency Updates**: Checks for outdated .NET and npm packages
- **Security Audit**: Scans for security vulnerabilities
- Creates automated PRs for dependency updates
- Creates security issues when vulnerabilities are found

## Setup Requirements

### Repository Secrets
No additional secrets are required. The workflows use the default `GITHUB_TOKEN`.

### Branch Protection
Consider setting up branch protection rules for `main`:
- Require status checks to pass before merging
- Require pull request reviews
- Restrict pushes to main branch

## Usage

### Creating a Release
1. **Automatic**: Push a git tag starting with `v` (e.g., `v1.0.0`)
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Manual**: Go to Actions -> Release -> Run workflow
   - Specify version (e.g., `v1.0.0`)
   - Choose whether to create GitHub release

### Running Workflows Manually
Most workflows support manual triggering:
- Go to your repository on GitHub
- Click "Actions" tab
- Select the workflow
- Click "Run workflow" button

### Monitoring Builds
- Check the "Actions" tab for workflow status
- Download build artifacts from successful runs
- Review logs for troubleshooting failures

## Artifacts

### CI Builds
- **Retention**: 30 days
- **Contents**: Executable, DLL folder, configuration files
- **Platforms**: win-x64, win-arm64

### Release Builds
- **Retention**: 90 days
- **Contents**: Complete packaged application with README
- **Platforms**: win-x64, win-arm64
- **Format**: ZIP files ready for distribution

## Customization

### Adding New Platforms
To add support for additional runtimes (e.g., linux-x64):

1. Update the matrix in `ci.yml` and `release.yml`:
   ```yaml
   strategy:
     matrix:
       runtime: [win-x64, win-arm64, linux-x64]
   ```

2. Ensure your build scripts support the new runtime

### Modifying Build Process
The workflows use your existing PowerShell build scripts:
- `scripts\build-app.ps1` - Main build script
- `scripts\full-setup.ps1` - Complete setup script
- `scripts\dev-setup.ps1` - Development setup

Modify these scripts to change the build process rather than the workflows.

### Caching
The workflows include caching for:
- npm packages (frontend dependencies)
- .NET NuGet packages (backend dependencies)

This significantly speeds up build times on subsequent runs.

## Troubleshooting

### Common Issues

1. **Build Script Failures**
   - Check PowerShell script syntax
   - Verify all required files are present
   - Review error logs in workflow output

2. **Dependency Issues**
   - Clear caches by re-running workflows
   - Check for version conflicts
   - Verify package.json and .csproj files

3. **Artifact Upload Failures**
   - Check file paths in workflow
   - Verify build outputs are created correctly
   - Review retention and size limits

### Getting Help
- Check workflow logs for detailed error messages
- Review the build documentation in `docs/build_and_deployment.md`
- Ensure your local build process works before debugging CI issues

## Best Practices

1. **Test Locally First**: Always test your build scripts locally before pushing
2. **Use Draft Releases**: For testing release workflows, create draft releases
3. **Monitor Dependencies**: Review automated dependency update PRs carefully
4. **Security**: Regularly check and address security audit findings
5. **Branch Strategy**: Use the PR validation workflow to catch issues early
