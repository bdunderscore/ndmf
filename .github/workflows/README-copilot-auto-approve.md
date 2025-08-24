# Copilot Auto-Approve Workflow

## Overview

This workflow automatically approves GitHub Actions workflow runs for Pull Requests submitted by GitHub Copilot bots, with security safeguards and resource optimization.

## How it works

### Trigger
- Triggers on push to `copilot/**` branches
- Uses `push` event instead of `pull_request_target` since the latter requires manual approval

### Security Checks
1. **Bot Validation**: Only approves PRs from `copilot-swe-agent[bot]` or `github-actions[bot]`
2. **File Safety**: Does NOT approve if any files under `.github/` are modified (security constraint)

### Process
1. **Find PR**: Locates the open PR associated with the copilot branch
2. **Validate Author**: Ensures PR is from approved GitHub Copilot bot
3. **Check Files**: Verifies no `.github/` files are modified
4. **Approve Workflows**: Auto-approves any workflow runs waiting for approval
5. **Clean Up**: Cancels previous workflow runs for the same branch to save resources

### Permissions
- `actions: write` - Required to approve and cancel workflow runs  
- `contents: read` - Required to read repository content
- `pull-requests: read` - Required to fetch PR details and file changes

### Resource Optimization
- Cancels previous workflow runs for the same copilot branch when new commits are pushed
- Only cancels runs that are queued, in_progress, or waiting (not completed runs)

## Security Considerations

- **No .github modifications**: Prevents malicious changes to CI/CD configuration
- **Bot-only approval**: Only trusted GitHub Copilot bots can trigger auto-approval  
- **Minimal permissions**: Uses least-privilege access pattern
- **Branch isolation**: Only affects `copilot/**` branches

## Example Usage

When GitHub Copilot creates a PR:
1. Copilot pushes to `copilot/fix-issue-123` branch
2. This workflow automatically runs and approves pending CI workflows
3. The main CI workflows (like GameCI tests) can now run without manual approval
4. If Copilot pushes additional commits, previous workflow runs are cancelled to save resources

## Troubleshooting

If workflows are not being auto-approved, check:
1. Is the branch named `copilot/*`?
2. Is the PR author `copilot-swe-agent[bot]` or `github-actions[bot]`?
3. Does the PR modify any files under `.github/`?
4. Are there actually workflow runs in "waiting" status?

The workflow provides detailed logging for debugging these conditions.