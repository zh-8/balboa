// Authoring script: regenerates docs/architecture/diagrams/*.excalidraw. Run `npm run author`.
// The .excalidraw files are the committed source of truth; edit them in excalidraw.com if you
// prefer, then `make diagrams`. Re-running this script overwrites hand edits.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { Diagram } from './dsl.mjs';
import { DIAGRAMS_DIR } from '../lib.mjs';

const out = [];
const done = (d) => out.push(d);
const title = (d, t, sub) => { d.text(0, -50, t, { size: 24 }); if (sub) d.text(0, -20, sub, { size: 14, color: '#495057' }); };

// ---------------------------------------------------------------- 1. system context
{
  const d = new Diagram('system-context'); title(d, 'System context', 'Who and what Balboa talks to. Product domain: undefined.');
  d.box('dev', 0, 25, 200, 80, 'Developer / owner\n(zh-8, plus AI agents)', { color: 'orange' });
  d.box('users', 0, 220, 200, 80, 'End users\n(browser)', { color: 'orange' });
  d.box('balboa', 380, 170, 340, 200,
    'Balboa\n.NET 10 + Angular modular monolith\n\nProduct domain is NOT defined yet.\nSample module is a placeholder.', { color: 'blue' });
  d.box('gh', 840, 20, 300, 90, 'GitHub\nrepo zh-8/balboa, Actions CI/CD,\nOIDC token issuer', { color: 'orange' });
  d.box('az', 830, 190, 320, 90, 'Microsoft Azure (Italy North)\nContainer Apps, Postgres, Key Vault,\nStatic Web Apps', { color: 'orange' });
  d.box('ghcr', 840, 360, 300, 80, 'ghcr.io\npublic container images', { color: 'orange' });
  d.box('dns', 840, 500, 300, 80, 'GoDaddy DNS\ncairobits.com CNAME records', { color: 'orange' });
  d.arrow(['users', 'r', 0], ['balboa', 'l', -10], { label: 'HTTPS' });
  d.arrow(['dev', 'r', 0], ['gh', 'l', 0], { label: 'PRs, reviews, merges', midX: 540 });
  d.arrow(['gh', 'b', -60], ['az', 't', -60], { label: 'OIDC deploy\n(no secret)', color: 'purple' });
  d.arrow(['gh', 'r', 0], ['ghcr', 'r', 0], { via: [[1190, 65], [1190, 400]], label: 'push\nimage', lx: 1190 });
  d.arrow(['az', 'b', 60], ['ghcr', 't', 60], { label: 'pull by digest' });
  d.arrow(['balboa', 'r', -10], ['az', 'l', 0], { label: 'runs on', midX: 780, color: 'blue' });
  d.arrow(['dns', 'l', 0], ['balboa', 'b', 0], { label: 'resolves\nsubdomains' });
  d.legend(0, 420, [['orange', 'External system or actor'], ['blue', 'Balboa (this repo)'], ['purple', 'Identity / security path']]);
  done(d);
}

// ---------------------------------------------------------------- 2. containers
{
  const d = new Diagram('containers'); title(d, 'Containers', 'Runtime pieces in dev (names from infra/bicep/main.bicep) and the request path.');
  d.box('browser', 0, 255, 170, 90, 'User\nbrowser', { color: 'orange' });
  d.box('swa', 380, 40, 280, 110, 'stapp-balboa-dev\nStatic Web App (Free)\nAngular SPA, edge-served', { color: 'blue' });
  d.box('api', 380, 255, 280, 120, 'ca-balboa-api-dev-itn\nContainer App: Balboa.Host\nminimal APIs, scale-to-zero', { color: 'blue' });
  d.box('pg', 880, 240, 300, 110, 'psql-balboa-dev-itn\nPostgreSQL 17, B1ms\nschema per module, Entra-only', { color: 'green' });
  d.box('job', 880, 440, 300, 100, 'caj-balboa-migrate-dev-itn\nContainer Apps job\nBalboa.Host migrate (DDL)', { color: 'yellow' });
  d.box('kv', 880, 40, 300, 90, 'kv-balboa-dev-xxxx\nKey Vault (RBAC mode)', { color: 'purple' });
  d.box('ai', 380, 440, 280, 100, 'appi-balboa-dev-itn\nApp Insights + Log Analytics\n(OpenTelemetry export)', { color: 'grey' });
  d.box('localdb', 0, 440, 200, 100, 'docker compose\nlocal dev only:\nPostgres 17 (ADR-0019)', { color: 'grey', dashed: true });
  d.arrow(['browser', 't', 0], ['swa', 'l', 0], { label: '1  load SPA', lx: 105, ly: 100 });
  d.arrow(['browser', 'r', 0], ['api', 'l', 0], { label: '2  fetch + CORS\n(origin = SPA)', lx: 275, ly: 285, color: 'blue' });
  d.arrow(['api', 'r', 0], ['pg', 'l', 0], { label: '3  SQL, Entra token\nas password', color: 'green' });
  d.arrow(['job', 't', 0], ['pg', 'b', 0], { label: 'DDL as migrator\nidentity, before\nnew revision', color: 'green' });
  d.arrow(['api', 'b', 0], ['ai', 't', 0], { label: 'traces, logs,\nmetrics' });
  d.arrow(['api', 't', 100], ['kv', 'l', 0], { via: [[720, 255], [720, 85]], label: 'read secrets\n(app identity)', color: 'purple', lx: 720, ly: 170 });
  d.legend(0, 600, [['blue', 'Compute'], ['green', 'Data'], ['yellow', 'Run-once job'], ['purple', 'Security'], ['orange', 'Actor']]);
  done(d);
}

// ---------------------------------------------------------------- 3. module map
{
  const d = new Diagram('module-map'); title(d, 'Module map', 'Modules talk only through .Contracts. Schemas are private. Enforced by build and architecture tests.');
  d.box('host', 390, 20, 340, 60, 'Balboa.Host\ncalls only AddXModule / MapXEndpoints', { color: 'blue' });
  d.zone('za', 0, 130, 420, 330, 'Sample module (placeholder)');
  d.zone('zb', 700, 130, 420, 330, 'Future module X');
  d.box('ac', 240, 170, 160, 70, 'Sample.Contracts\nDTOs, IQueries,\nintegration events', { color: 'green', size: 14 });
  d.box('ai', 20, 280, 380, 70, 'Sample.Domain / Application / Infrastructure\n(internal types only)', { color: 'blue', size: 14 });
  d.box('as', 90, 390, 240, 50, 'schema: sample', { color: 'green', shape: 'rectangle' });
  d.box('bc', 720, 170, 160, 70, 'X.Contracts\nDTOs, IQueries,\nintegration events', { color: 'green', size: 14 });
  d.box('bi', 720, 280, 380, 70, 'X.Domain / Application / Infrastructure\n(internal types only)', { color: 'blue', size: 14 });
  d.box('bs', 790, 390, 240, 50, 'schema: x', { color: 'green' });
  d.arrow(['host', 'b', -120], [200, 130, 't'], { color: 'blue' });
  d.arrow(['host', 'b', 120], [910, 130, 't'], { color: 'blue' });
  d.arrow(['bi', 'l', -20], ['ac', 'r', 0], { color: 'green', label: 'ALLOWED\nquery via IXQueries / DTOs', midX: 560, lx: 560, ly: 205 });
  d.arrow(['ai', 'r', 15], ['bi', 'l', 15], { color: 'red', dashed: true, label: 'FORBIDDEN\nreference to another module\'s\ninternals: build fails', lx: 560, ly: 332 });
  d.arrow(['as', 'r', 0], ['bs', 'l', 0], { color: 'red', dashed: true, label: 'FORBIDDEN\ncross-schema FK or join', lx: 560, ly: 420 });
  d.box('bb', 330, 520, 440, 80, 'BuildingBlocks: outbox + inbox, in-process dispatch\nintegration events cross here after commit', { color: 'yellow', size: 14 });
  d.box('sk', 300, 650, 520, 50, 'SharedKernel: Entity, AggregateRoot, Result (all modules may use)', { color: 'grey', size: 14 });
  d.arrow(['as', 'b', 0], ['bb', 'l', -10], { color: 'blue', label: 'outbox row,\nsame tx', lx: 210, ly: 495 });
  d.arrow(['bb', 'r', -10], ['bs', 'b', 0], { color: 'blue', label: 'delivered after commit,\ninbox dedupes', lx: 910, ly: 495 });
  d.legend(0, 560, [['green', 'Public surface / data'], ['blue', 'Internal code'], ['yellow', 'Event infrastructure'], ['red', 'Forbidden edge']]);
  done(d);
}

// ---------------------------------------------------------------- 4. module internals
{
  const d = new Diagram('module-internals'); title(d, 'Module internals', 'Arrows read "may reference". Domain depends only on SharedKernel.');
  d.box('infra', 230, 20, 440, 100, 'Infrastructure\nDbContext, migrations, minimal-API endpoints,\nAddXModule / MapXEndpoints (the only public API)', { color: 'blue' });
  d.box('app', 230, 190, 440, 100, 'Application\ncommand and query handlers (internal sealed),\nports (interfaces), validation', { color: 'blue' });
  d.box('dom', 0, 380, 360, 120, 'Domain\naggregates, value objects, domain events\nPURE: no EF, no ASP.NET, no IO', { color: 'green' });
  d.box('con', 560, 380, 360, 120, 'Contracts\nintegration events, query interfaces, DTOs\nrecords and interfaces only', { color: 'green' });
  d.box('sk', 250, 560, 400, 90, 'SharedKernel\nEntity, AggregateRoot, ValueObject,\nIDomainEvent, Result', { color: 'grey' });
  d.arrow(['infra', 'b', 0], ['app', 't', 0], { color: 'green' });
  d.arrow(['app', 'l', 0], ['dom', 't', 0], { color: 'green' });
  d.arrow(['app', 'b', 150], ['con', 't', -100], { color: 'green' });
  d.arrow(['infra', 'l', -10], ['dom', 't', -100], { color: 'green', via: [[60, 60]] });
  d.arrow(['infra', 'r', 10], ['con', 't', 120], { color: 'green', via: [[860, 80]] });
  d.arrow(['dom', 'b', 0], ['sk', 'l', 0], { color: 'green' });
  d.arrow(['con', 'b', 0], ['sk', 'r', 0], { color: 'green' });
  d.box('x1', 655, 195, 170, 90, 'Other modules:\nreference ONLY\nthis Contracts', { color: 'orange', size: 14 });
  d.arrow(['x1', 'b', 0], ['con', 't', 0], { color: 'orange', dashed: true });
  d.arrow(['dom', 't', 100], ['app', 'b', -170], { color: 'red', dashed: true, label: 'FORBIDDEN', lx: 335, ly: 352 });
  d.text(0, 680, 'FORBIDDEN (build or architecture test fails):', { size: 14, color: '#c92a2a' });
  d.text(0, 704, '  Domain -> Application / Infrastructure / EF / ASP.NET', { size: 13, color: '#c92a2a' });
  d.text(0, 724, '  Application -> Infrastructure (same module)', { size: 13, color: '#c92a2a' });
  d.text(0, 744, '  Anything -> another module\'s Domain, Application or Infrastructure', { size: 13, color: '#c92a2a' });
  d.text(0, 764, '  Contracts -> anything but SharedKernel; no classes, only records/interfaces', { size: 13, color: '#c92a2a' });
  done(d);
}

// ---------------------------------------------------------------- 5. azure deployment
{
  const d = new Diagram('azure-deployment'); title(d, 'Azure deployment', 'Italy North unless noted. Names from infra/. Prod is deferred (ADR-0018).');
  const X = 100; // dev group x-offset
  d.zone('zs', 0, 20, 330, 440, 'rg-balboa-shared-itn');
  d.box('log', 20, 60, 290, 80, 'log-balboa-shared-itn\nLog Analytics workspace', { color: 'grey' });
  d.box('gh', 20, 190, 290, 100, 'id-balboa-gh-dev\nGitHub deploy identity\n+ federated credential', { color: 'purple' });
  d.text(20, 310, 'Its RBAC (set by bootstrap):\n- Contributor on rg-balboa-dev-itn\n- RBAC Administrator on dev, ABAC-limited\n  to Key Vault Secrets User and\n  Monitoring Metrics Publisher\n- Reader on rg-balboa-shared-itn', { size: 12, color: '#5f3dc4' });
  d.zone('zd', 330 + X, 20, 840, 640, 'rg-balboa-dev-itn');
  d.box('cae', 350 + X, 60, 440, 200, '', { color: 'blue', dashed: true });
  d.text(364 + X, 66, 'cae-balboa-dev-itn  (Container Apps env, Consumption-only)', { size: 13, color: '#1864ab' });
  d.box('api', 370 + X, 110, 190, 80, 'ca-balboa-api-dev-itn\nAPI Container App', { color: 'blue', size: 14 });
  d.box('job', 580 + X, 110, 190, 80, 'caj-balboa-migrate-\ndev-itn  (job)', { color: 'yellow', size: 14 });
  d.box('swa', 830 + X, 60, 300, 90, 'stapp-balboa-dev\nStatic Web App (West Europe)', { color: 'blue', size: 14 });
  d.box('pg', 370 + X, 330, 200, 80, 'psql-balboa-dev-itn\nPostgres B1ms, Entra-only', { color: 'green', size: 14 });
  d.box('kv', 620 + X, 330, 190, 80, 'kv-balboa-dev-xxxx\nKey Vault', { color: 'purple', size: 14 });
  d.box('appi', 860 + X, 330, 270, 80, 'appi-balboa-dev-itn\nApp Insights', { color: 'grey', size: 14 });
  d.box('idm', 360 + X, 480, 170, 80, 'id-balboa-migrator-dev\nmigrator identity', { color: 'purple', size: 12 });
  d.box('ida', 640 + X, 480, 150, 80, 'id-balboa-api-dev\nAPI identity', { color: 'purple', size: 12 });
  d.arrow(['gh', 'r', 0], [330 + X, 240, 'l'], { color: 'purple', label: 'deploys with\nthese rights', lx: 380, ly: 205 });
  d.arrow(['idm', 't', -25], ['pg', 'b', -25], { color: 'purple', label: 'DDL', lx: 440 + X - 60, ly: 445 });
  d.arrow(['ida', 'l', 0], ['pg', 'b', 50], { color: 'purple', label: 'DML only,\nEntra token', lx: 590 + X, ly: 535 });
  d.arrow(['ida', 't', 0], ['kv', 'b', 0], { color: 'purple', label: 'Key Vault\nSecrets User', lx: 715 + X + 60, ly: 445 });
  d.arrow(['ida', 'r', 0], ['appi', 'b', 0], { color: 'purple', label: 'Monitoring Metrics\nPublisher', lx: 920 + X, ly: 535 });
  d.text(360 + X, 590, 'Identities are attached to the API app and the migrate job.\nMigrator holds DDL rights on module schemas; the API identity holds DML only.', { size: 13 });
  d.legend(0, 500, [['blue', 'Compute / hosting'], ['green', 'Data'], ['purple', 'Identity / security'], ['yellow', 'Job'], ['grey', 'Observability']]);
  done(d);
}
export { out, done, title };

if (process.argv[1].endsWith('diagrams.mjs')) {
  const more = await import('./diagrams2.mjs');
  for (const d of [...out, ...more.out]) {
    writeFileSync(join(DIAGRAMS_DIR, `${d.name}.excalidraw`), JSON.stringify(d.scene(), null, 2) + '\n');
    console.log(`wrote ${d.name}.excalidraw (${d.els.length} elements)`);
  }
}
