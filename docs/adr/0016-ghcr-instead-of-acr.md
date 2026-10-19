# ADR-0016: ghcr.io instead of Azure Container Registry

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Container Apps pulls images from a registry. The usual choice is Azure Container Registry
(ACR). ACR Basic costs $0.1666 per day, about **$5.07 per month in Italy North**, and no
free tier or free grant covers it. On a project targeting about $0 per month that is the
single largest recurring line. The Balboa repository is public, and GitHub Container
Registry (ghcr.io) is free for public packages.

## Decision drivers

- Zero recurring cost.
- Images built in GitHub Actions, which already authenticates to ghcr.io with `GITHUB_TOKEN`.
- Container Apps must be able to pull without stored credentials where possible.
- Reversibility: switching registry must be cheap.

## Considered options

1. Azure Container Registry Basic.
2. ghcr.io with public images.
3. Docker Hub.

## Decision

Publish images to **ghcr.io** (`ghcr.io/zh-8/balboa-*`). Because the repository is public, the
packages are public and Container Apps pulls them anonymously, so there is **no registry
credential to store or rotate**. Images are tagged with the CalVer release and the commit
SHA, and deploys reference an immutable tag or digest, never `latest`.

The source code is public for reference, but the licence is all rights reserved, so images
must not contain secrets; configuration comes from environment variables and managed
identity.

## Consequences

### Good

- Saves about $5.07 per month, which is the whole runtime budget of the project.
- No pull credential, hence nothing to rotate.
- Build, publish and source live in one place with the same access control.

### Bad

- Images are publicly pullable, so anyone can download the compiled application. This is
  acceptable only while the repository is public and the code has no secrets.
- Pulls cross from Azure to GitHub; a GitHub outage or rate limit can block scale-out or
  rollback of a revision whose image is not cached.
- No Azure-native vulnerability scanning or geo-replication that ACR offers.
- Anonymous pull is not private-network, so it cannot combine with a locked-down network
  design later.

## Revisit trigger

Reopen when a production environment is created, or when private images are needed (for
example the repository goes private or the build includes proprietary assets). At that point
compare ACR Basic against private ghcr.io with a pull credential.
