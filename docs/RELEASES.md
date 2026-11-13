# GitHub Releases

GitHub Releases are created on demand from the public repository `Raresoft-Group/File-Permission-Reporter`. Pushing to `main` does not create a release.

## Maintainer release flow

1. Push the intended application changes to `main` and ask Codex to prepare a release.
2. Codex reviews commits on remote `main` since the last Release, recommends a stable SemVer version and summarizes the changes.
3. Confirm the proposed version and release summary. Codex then dispatches the `Publish release` workflow against `main` with a tag such as `v1.2.3`.
4. The Windows workflow runs the acceptance tests, builds the versioned self-contained `win-x64` ZIP, stages a draft release, attaches and verifies the ZIP, then publishes it.

Codex uses the GitHub CLI with an authenticated account that has the `workflow` scope. The workflow uses the built-in `GITHUB_TOKEN`; no personal access token or repository secret is required. The tag and release are tied to the exact `main` commit selected when the workflow is dispatched. Local uncommitted files are never included.

## Manual dispatch

The workflow file must first be merged into the repository's default branch. From an authenticated GitHub CLI session, a maintainer can start it with:

```powershell
gh workflow run release.yml --repo Raresoft-Group/File-Permission-Reporter --ref main -f version=v1.2.3
gh run list --repo Raresoft-Group/File-Permission-Reporter --workflow release.yml --limit 1
gh run watch RUN_ID --repo Raresoft-Group/File-Permission-Reporter --exit-status
```

Only stable `vMAJOR.MINOR.PATCH` versions are accepted. Pre-release and build-metadata suffixes are not supported. A version cannot reuse an existing tag or published release. An interrupted draft release can only be resumed when its tag points to the same commit as the current workflow run; its versioned ZIP is safely replaced before publishing.

## Release contents and local builds

Each published GitHub Release contains `FilePermissionReporter-vX.Y.Z-win-x64.zip`, with the self-contained EXE, README and GPL license text. The executable Windows file version is `X.Y.Z.0`; its product version starts with `X.Y.Z` and includes the source revision when available. GitHub also provides source archives for the tagged source commit.

Local builds remain available:

```powershell
./publish-portable.ps1
./publish-portable.ps1 -Version 1.2.3
```

Without `-Version`, the script retains the existing `artifacts/FilePermissionReporter-win-x64.zip` name. A versioned build creates `artifacts/FilePermissionReporter-v1.2.3-win-x64.zip` and embeds version `1.2.3` in the executable metadata.
