# ADR-0007: Angular on Azure Static Web Apps Free tier, hosted in West Europe

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

The Angular frontend is static output after build. It needs hosting with HTTPS, a custom
domain and global edge delivery, at zero cost. Azure Static Web Apps (SWA) has a Free tier.
SWA is not offered in Italy North, our chosen region (ADR-0008), so its control-plane
location must be a different region.

## Decision drivers

- Zero cost.
- Custom domains with managed certificates (the domain is on GoDaddy DNS).
- Deploy from GitHub Actions without stored secrets where possible.
- Stay inside Azure to keep one billing and identity surface.

## Considered options

1. Serve the SPA from the API container.
2. Azure Storage static website plus CDN.
3. Azure Static Web Apps, Free tier.
4. Non-Azure hosting (GitHub Pages, Cloudflare Pages, Netlify).

## Decision

Host the Angular app on **Azure Static Web Apps, Free tier**, with the resource located in
**West Europe**, the nearest supported region. Content is served from the edge, so the
resource location affects the control plane and not end-user latency. The API is addressed on
a separate subdomain (`api.<env>.balboa.cairobits.com`), so the API must allow the SPA origin
via CORS.

## Consequences

### Good

- No cost, managed TLS, global distribution and PR preview environments are built in.
- Separating static assets from the API keeps the container's free grant for API traffic.
- Custom domains are CNAMEs at GoDaddy; no Azure DNS zone is needed.

### Bad

- The SWA resource sits in a different region from the rest of the stack, which breaks the
  "everything in `itn`" naming rule for that one resource.
- The Free tier has limits (bandwidth and feature caps, no SLA) that we accept for now.
- Cross-origin API calls require CORS configuration, and cookies need careful SameSite
  handling if cookie auth is ever used.
- Another vendor-specific deployment path (the SWA deployment token) must be handled; it is
  a secret and must live in GitHub environment secrets, not git.

## Revisit trigger

Reopen if SWA becomes available in Italy North, if the Free tier limits are hit, or if we
need server-side rendering or an SLA, which moves us to the Standard tier or another host.
