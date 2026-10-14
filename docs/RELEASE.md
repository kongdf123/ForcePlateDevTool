# ForcePlateDevTool Release Guide

This document describes how to publish a new version of ForcePlateDevTool and how the automated release pipeline works.

## Overview

ForcePlateDevTool uses a **Git-tag-driven release model**. A developer publishes a new version by creating and pushing a correctly formatted Git tag. GitHub Actions then automatically validates the tag, builds and tests the application, packages it into a portable ZIP, calculates its SHA-256 checksum, and publishes the artifacts to a private GitHub Release.

Release flow:

```
Git tag → validation → build and test → self-contained win-x64 publish → portable ZIP → SHA-256 checksum → private GitHub Release
```

## Versioning Policy

ForcePlateDevTool follows [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`.

| Component | When to increment |
|---|---|
| **MAJOR** | Incompatible changes that require users, configurations, or integrations to adapt. |
| **MINOR** | Backward-compatible new functionality. |
| **PATCH** | Backward-compatible bug fixes or corrections. |

Release tags must use the format `vMAJOR.MINOR.PATCH` (for example, `v1.0.0`, `v1.1.0`, `v2.0.0`). Prerelease tags (such as `v1.2.0-rc.1`) are **not** accepted by the release pipeline.

## Developer Release Procedure

### Step 1 — Prepare the release commit

Merge the intended changes into `main` and make sure the continuous-integration workflow passes.

### Step 2 — Update the local repository

```bash
git switch main
git pull --ff-only origin main
```

### Step 3 — Create an annotated tag

Choose the next version according to the versioning policy and create an annotated tag. For example, to publish version `1.0.0`:

```bash
git tag -a v1.0.0 -m "ForcePlateDevTool 1.0.0"
```

### Step 4 — Push the tag

```bash
git push origin v1.0.0
```

Pushing the tag triggers the release workflow automatically.

### Step 5 — Monitor the release workflow

Open the repository's **Actions** page on GitHub and verify that the `ForcePlateDevTool Release` workflow completes successfully. If any step fails, the release is not published.

### Step 6 — Verify the GitHub Release

Confirm that the release on GitHub contains:

- The expected version tag.
- The portable ZIP (`ForcePlateDevTool-vX.Y.Z-win-x64.zip`).
- The SHA-256 checksum file (`ForcePlateDevTool-vX.Y.Z-win-x64.zip.sha256`).
- Auto-generated release notes.

Download the ZIP and test it on a clean Windows x64 machine or virtual machine before distributing it internally.

### Step 7 — Preserve release history

Never move, overwrite, or reuse a published version tag. If a released build contains a defect, publish a new patch version, such as `v1.0.1`.

## Release Artifacts

Each successful release contains the following assets.

### Portable ZIP

`ForcePlateDevTool-vX.Y.Z-win-x64.zip`

The ZIP contains the complete self-contained application output for `win-x64`, including `ForcePlateDevTool.exe`, the .NET runtime, and all dependencies. Users extract the ZIP, keep the files together, and run `ForcePlateDevTool.exe`. No separate .NET installation is required.

### VERSION.txt

Included inside the ZIP. Contains:

- Application name
- Semantic version
- Informational version
- Git tag
- Full source commit SHA
- Runtime identifier (`win-x64`)
- Deployment model (`self-contained`)
- UTC build timestamp

### README.txt

A short file inside the ZIP with extraction and run instructions.

### SHA-256 Checksum

`ForcePlateDevTool-vX.Y.Z-win-x64.zip.sha256`

The checksum is calculated from the final ZIP artifact. Recalculate the ZIP's SHA-256 and compare it with the published value to detect accidental corruption or modification.

> A checksum alone does not prove publisher authenticity. Code signing or a trusted signature-verification mechanism would be needed for stronger publisher verification.

## How the Release Pipeline Works

The pipeline (`.github/workflows/release.yml`) performs the following steps:

1. **Checkout** — fetches the full history and checks out the tagged commit.
2. **Validate release tag** — ensures the tag matches `vMAJOR.MINOR.PATCH`. Rejects prerelease or malformed tags.
3. **Verify commit reachability** — confirms the tagged commit is an ancestor of `origin/main`, preventing accidental releases from unrelated branches.
4. **Setup .NET** — installs the .NET 8 SDK.
5. **Restore solution** — restores NuGet dependencies.
6. **Run tests** — runs the test suite in Release configuration. Tests must pass before publishing.
7. **Publish application** — publishes a self-contained `win-x64` build to `artifacts/publish`, embedding the release version and source commit into the assembly metadata.
8. **Create VERSION.txt and README.txt** — writes metadata and user instructions into the publish output.
9. **Create ZIP package** — packages the publish output into the portable ZIP.
10. **Generate SHA-256 checksum** — computes the checksum of the final ZIP.
11. **Create GitHub Release** — creates a private GitHub Release and uploads the ZIP and checksum.

If validation, build, tests, or packaging fail, the release is not created.

## Troubleshooting

| Problem | Possible cause | Resolution |
|---|---|---|
| Workflow fails at "Validate release tag" | Tag does not match `vMAJOR.MINOR.PATCH` | Use the correct tag format, e.g. `v1.0.0`. |
| Workflow fails at "Verify tagged commit is reachable from main" | Tag was created on a branch not merged into `main` | Merge the branch into `main`, then create and push a new tag on `main`. |
| Tests fail | The tagged source has failing tests | Fix the tests, push the fix to `main`, then create and push a new tag. |
| Publish fails | Build errors or unsupported runtime | Check the build output; fix issues, then retag. |
| Release not visible | The repository is private | Only users with repository access can see and download the release assets. |

## Security and Operational Notes

- Protect the `main` branch and control who can create or push release tags.
- Consider restricting release-tag creation to authorized maintainers.
- Use the minimum permissions necessary for GitHub Actions (`contents: write`).
- Do not store secrets in source code, release metadata, or packaged configuration files.
- Do not include private device credentials or sensitive operational data in the ZIP.
- Test the portable package on a clean Windows x64 environment before internal distribution.