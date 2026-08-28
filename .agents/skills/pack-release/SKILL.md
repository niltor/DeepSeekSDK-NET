---
name: pack-release
description: Update this repository's NuGet package version and release notes, then prepare the changes for merge into main so CI can publish the release.
metadata:
  short-description: Bump version and release through main
---

# NuGet Pack and Release

Use this skill when the user asks to pack or release a new version. Read the root `AGENTS.md` first. The release operation has two steps; restore, test, pack, and NuGet publication are handled automatically by CI after the change reaches `main`.

## Package facts

Update both publishable projects together:

| Project | Package ID | Version field |
| --- | --- | --- |
| `src/DeepSeek.Core/DeepSeek.Core.csproj` | `Ater.DeepSeek.Core` | `<Version>` |
| `src/DeepSeek.AspNetCore/DeepSeek.AspNetCore.csproj` | `Ater.DeepSeek.AspNetCore` | `<Version>` |

The current package version is `1.6.0`. Unless the user explicitly supplies a version, increment the SemVer patch number by one (`1.6.0` → `1.6.1`). Keep the two `<Version>` values identical and do not add a leading `v`. Update `PackageReleaseNotes` in both projects to describe the release.

Do not change Core's explicit `AssemblyVersion`/`FileVersion` (`0.1.0.0`) during an ordinary NuGet release. Git tags are not the version source and do not trigger publication; existing tags use both `vX.Y.Z` and `X.Y.Z` forms.

## Two-step release

1. **Set the package version and release notes.** Inspect both `.csproj` files, preserve unrelated user changes, choose the explicitly requested version or increment the current patch number, then update `<Version>` and `PackageReleaseNotes` in both files.

2. **Merge the changes into `main`.** Commit the synchronized project-file changes through the normal review process, merge/push them to `main`, create a tag named exactly after the new version (without a `v` prefix), push the tag, and finally check out `dev` after the merge. The tag is a version marker; it does not trigger CI.
