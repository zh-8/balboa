// Second half of the diagram authoring script (see diagrams.mjs).
import { Diagram } from './dsl.mjs';
export const out = [];
const done = (d) => out.push(d);
const title = (d, t, sub) => { d.text(0, -50, t, { size: 24 }); if (sub) d.text(0, -20, sub, { size: 14, color: '#495057' }); };

// ---------------------------------------------------------------- 6. OIDC flow
{
  const d = new Diagram('oidc-flow'); title(d, 'GitHub OIDC to Azure', 'No client secret is stored anywhere. Every credential here is short-lived or non-secret.');
  d.box('job', 0, 100, 280, 110, 'GitHub Actions job\nenvironment: dev\npermissions: id-token: write', { color: 'orange' });
  d.box('iss', 460, 100, 340, 130, 'GitHub OIDC issuer\ntoken.actions.githubusercontent.com\nsigns a short-lived JWT, sub =\nrepo:zh-8/balboa:environment:dev', { color: 'orange' });
  d.box('entra', 860, 100, 320, 130, 'Microsoft Entra ID\nfederated credential on\nid-balboa-gh-dev: checks issuer,\naudience and exact subject', { color: 'purple' });
  d.box('mi', 860, 310, 320, 90, 'id-balboa-gh-dev\nuser-assigned managed identity', { color: 'purple' });
  d.box('arm', 400, 480, 360, 110, 'Azure Resource Manager\nrg-balboa-dev-itn: Contributor +\nconstrained RBAC Admin; Reader on shared', { color: 'blue' });
  d.arrow(['job', 'r', 0], ['iss', 'l', 0], { both: true, label: '1  request / receive\nsigned JWT', color: 'orange' });
  d.arrow(['job', 't', 0], ['entra', 't', 0], { midY: 50, label: '2  azure/login sends JWT\n(client id, tenant id, subscription id: not secret)', color: 'purple', lx: 560, ly: 50 });
  d.arrow(['entra', 'b', 0], ['mi', 't', 0], { label: '3  subject matches\nthe credential', color: 'purple' });
  d.arrow(['mi', 'l', 0], ['job', 'b', 0], { label: '4  Entra access token\n(short-lived) returned to the job', color: 'purple', lx: 560, ly: 355 });
  d.arrow(['job', 'b', 60], ['arm', 'l', 0], { label: '5  call ARM with token', color: 'blue', lx: 240, ly: 520 });
  d.box('note', 860, 480, 320, 120, 'Why this is safe\nNo secret exists to leak or rotate.\nThe subject is pinned to the GitHub\nenvironment: a branch or fork cannot\nmint a usable token.', { color: 'green', size: 14 });
  done(d);
}

// ---------------------------------------------------------------- 7. CI/CD
{
  const d = new Diagram('cicd-pipeline'); title(d, 'CI/CD pipeline', 'Build once on main, deploy the same image digest forward. Workflows: ci.yml, main.yml, _deploy.yml.');
  const W = 220, H = 90;
  d.text(0, 6, 'Pull request', { size: 16, color: '#495057' });
  d.box('pr', 0, 30, W, H, 'Push short-lived branch,\nopen PR (Spec: line)', { color: 'grey' });
  d.box('ci', 270, 30, W, H, 'ci.yml jobs: fmt, lint,\nunit/arch/FE tests, spec-check,\ndiagrams-check, infra-lint', { color: 'yellow', size: 13 });
  d.box('ok', 540, 30, W, H, 'ci-ok\nthe only required\nstatus check', { color: 'green' });
  d.box('merge', 810, 30, W, H, 'Squash merge\nto main', { color: 'grey' });
  d.arrow(['pr', 'r', 0], ['ci', 'l', 0]); d.arrow(['ci', 'r', 0], ['ok', 'l', 0]); d.arrow(['ok', 'r', 0], ['merge', 'l', 0]);
  d.text(0, 170, 'Main (main.yml)', { size: 16, color: '#495057' });
  d.box('build', 810, 200, W, H, 'Build + test\nbackend and frontend', { color: 'blue' });
  d.box('push', 540, 200, W, H, 'Publish image\nto ghcr.io', { color: 'orange' });
  d.box('dig', 270, 200, W, H, 'Resolve image digest\n(sha256:...), never a tag', { color: 'yellow' });
  d.box('dep', 0, 200, W, H, 'Deploy dev (_deploy.yml)\nenvironment: dev, OIDC login,\ndigest-pinned guard', { color: 'purple', size: 13 });
  d.arrow(['merge', 'b', 0], ['build', 't', 0]); d.arrow(['build', 'l', 0], ['push', 'r', 0]);
  d.arrow(['push', 'l', 0], ['dig', 'r', 0]); d.arrow(['dig', 'l', 0], ['dep', 'r', 0]);
  d.text(0, 350, 'Deploy (inside the dev job)', { size: 16, color: '#495057' });
  d.box('wi', 0, 380, W, H, 'Bicep what-if,\ndeploy infrastructure', { color: 'purple' });
  d.box('mig', 270, 380, W, H, 'Run migrate job\n(migrator identity, DDL)\nBEFORE traffic', { color: 'yellow' });
  d.box('rdy', 540, 380, W, H, 'Wait for API ready,\ndeploy Angular to\nStatic Web App', { color: 'blue' });
  d.box('smoke', 810, 380, W, H, 'Playwright smoke tests\n(post-deploy only,\nnever in the PR gate)', { color: 'green', size: 14 });
  d.arrow(['dep', 'b', 0], ['wi', 't', 0]); d.arrow(['wi', 'r', 0], ['mig', 'l', 0]);
  d.arrow(['mig', 'r', 0], ['rdy', 'l', 0]); d.arrow(['rdy', 'r', 0], ['smoke', 'l', 0]);
  d.box('prod', 1080, 380, 260, H, 'Prod: promote the SAME digest\nDEFERRED (ADR-0018):\nno prod environment yet', { color: 'red', dashed: true, size: 14 });
  d.arrow(['smoke', 'r', 0], ['prod', 'l', 0], { color: 'red', dashed: true });
  d.legend(0, 520, [['yellow', 'Pipeline step'], ['purple', 'Uses OIDC identity'], ['green', 'Gate / check'], ['red', 'Deferred']]);
  done(d);
}

// ---------------------------------------------------------------- 8. branching
{
  const d = new Diagram('branching-flow'); title(d, 'Trunk-based branching', 'main is always releasable. Branches live hours to a couple of days.');
  const c = (id, x, y, color = 'blue') => d.box(id, x, y, 40, 40, '', { shape: 'ellipse', color });
  d.box('flag', 0, 20, 360, 100, 'Unfinished work merges DARK\nbehind a feature flag.\nNo long-lived branches, no develop branch.', { color: 'yellow', size: 14 });
  d.arrow([60, 240, 'r'], [1180, 240, 'r'], { color: 'blue', sw: 3 });
  d.text(0, 190, 'main', { size: 16, color: '#1864ab' });
  c('m0', 20, 220); c('s1', 360, 220, 'green'); c('s2', 700, 220, 'green'); c('s3', 1040, 220, 'green');
  const branch = (n, from, x0, label, to) => {
    c(`${n}1`, x0, 360, 'grey'); c(`${n}2`, x0 + 90, 360, 'grey'); c(`${n}3`, x0 + 180, 360, 'grey');
    d.arrow([from, 'b', 10], [`${n}1`, 'l', 0], { color: 'grey' });
    d.arrow([`${n}1`, 'r', 0], [`${n}2`, 'l', 0], { color: 'grey' }); d.arrow([`${n}2`, 'r', 0], [`${n}3`, 'l', 0], { color: 'grey' });
    d.arrow([`${n}3`, 'r', 0], [to, 'b', -10], { color: 'green', label: 'squash merge\nci-ok required' });
    d.text(x0 - 10, 415, label, { size: 13, color: '#495057' });
  };
  branch('a', 'm0', 70, 'feat/short-lived (PR, small, reviewed)', 's1');
  branch('b', 's1', 410, 'fix/other-change (PR)', 's2');
  branch('c', 's2', 750, 'chore/docs-update (PR)', 's3');
  d.box('tag', 1000, 100, 260, 80, 'Tag vYYYY.MM.DD.N (CalVer)\non a green main commit', { color: 'purple', size: 14 });
  d.arrow(['tag', 'b', 0], ['s3', 't', 0], { color: 'purple' });
  d.text(0, 480, 'One squash commit per PR keeps main linear (conventional commits). Never force-push main.', { size: 14, color: '#495057' });
  done(d);
}

// ---------------------------------------------------------------- 9. outbox
{
  const d = new Diagram('outbox-flow'); title(d, 'Transactional outbox and inbox', 'Integration events leave a module only if the change commits; handling is idempotent.');
  d.box('cmd', 0, 60, 210, 90, '1  Command\nAPI endpoint to\ncommand handler', { color: 'blue' });
  d.zone('tx', 260, 20, 800, 200, 'One database transaction (the module\'s DbContext)', { color: 'green' });
  d.box('agg', 280, 60, 200, 90, '2  Aggregate\nenforces invariants,\nraises events', { color: 'blue' });
  d.box('rows', 540, 60, 220, 90, '3  Write state +\nOUTBOX row\n(same transaction)', { color: 'green' });
  d.box('commit', 820, 60, 220, 90, '4  COMMIT\nboth rows or neither', { color: 'yellow' });
  d.text(280, 172, 'Domain events stay inside the module and dispatch BEFORE commit.', { size: 13 });
  d.arrow(['cmd', 'r', 0], ['agg', 'l', 0]); d.arrow(['agg', 'r', 0], ['rows', 'l', 0]); d.arrow(['rows', 'r', 0], ['commit', 'l', 0]);
  d.box('disp', 820, 300, 220, 100, '5  Dispatcher\nin-process, after commit:\nreads outbox, publishes', { color: 'blue' });
  d.box('inbox', 540, 300, 220, 100, '6  Subscriber INBOX\nhave I handled this\nmessage id already?', { color: 'purple' });
  d.box('hand', 280, 300, 200, 100, '7  Handler runs;\ninbox row written in\nthe same transaction', { color: 'blue', size: 14 });
  d.box('skip', 540, 480, 220, 70, 'Skip: duplicate delivery\nis harmless', { color: 'grey', size: 14 });
  d.box('mark', 820, 480, 220, 70, 'Mark outbox row\ndelivered', { color: 'green', size: 14 });
  d.arrow(['commit', 'b', 0], ['disp', 't', 0], { label: 'after commit' });
  d.arrow(['disp', 'l', 0], ['inbox', 'r', 0]);
  d.arrow(['inbox', 'l', 0], ['hand', 'r', 0], { label: 'no: first time', color: 'green' });
  d.arrow(['inbox', 'b', 0], ['skip', 't', 0], { label: 'yes: seen', color: 'red' });
  d.arrow(['disp', 'b', 0], ['mark', 't', 0]);
  d.box('sweep', 0, 620, 1040, 90, 'Startup sweep: every host start re-publishes outbox rows that were committed but never delivered\n(crash between commit and dispatch). Scale-to-zero means a background poller cannot be relied on.\nDelivery is at-least-once; the inbox makes the effect exactly-once.', { color: 'yellow', size: 14 });
  d.arrow(['sweep', 'r', -20], ['disp', 'r', 0], { via: [[1090, 645], [1090, 350]], label: 're-publish', lx: 1090, ly: 520 });
  done(d);
}

// ---------------------------------------------------------------- 10. spec-driven loop
{
  const d = new Diagram('spec-driven-loop'); title(d, 'Spec-driven loop', 'Intent first, proof by tag, enforced by make spec-check (ADR-0013).');
  d.box('spec', 0, 60, 280, 120, '1  Write the spec (and ADR)\ndocs/specs/SPEC-NNNN/spec.md\nnumbered criteria AC-NNNN-01\n(Given / When / Then)', { color: 'blue', size: 14 });
  d.box('red', 340, 60, 280, 120, '2  Failing test first (RED)\ntagged with the criterion:\n[Spec("AC-0001-02")] in .NET\n"[AC-0001-02] ..." in Vitest', { color: 'red', size: 14 });
  d.box('green', 680, 60, 280, 120, '3  Make it pass (GREEN),\nthen refactor\nsmall commits on a\nshort-lived branch', { color: 'green', size: 14 });
  d.box('pr', 680, 280, 280, 110, '4  Open the PR\nbody contains a line\nSpec: SPEC-NNNN', { color: 'grey' });
  d.box('chk', 340, 280, 280, 110, '5  CI runs make spec-check', { color: 'yellow' });
  d.box('merge', 0, 280, 280, 110, '6  ci-ok green,\nsquash merge to main', { color: 'green' });
  d.arrow(['spec', 'r', 0], ['red', 'l', 0]); d.arrow(['red', 'r', 0], ['green', 'l', 0]);
  d.arrow(['green', 'b', 0], ['pr', 't', 0]); d.arrow(['pr', 'l', 0], ['chk', 'r', 0]); d.arrow(['chk', 'l', 0], ['merge', 'r', 0], { label: 'pass', color: 'green' });
  d.arrow(['merge', 'l', 0], ['spec', 'l', 0], { via: [[-50, 335], [-50, 120]], dashed: true, label: 'behaviour changes?\nupdate the spec in the same PR', lx: -50, ly: 230 });
  d.arrow(['chk', 't', 0], ['red', 'b', 0], { color: 'red', dashed: true, label: 'fail: add or fix\nthe tagged test', lx: 480, ly: 230 });
  d.box('rules', 340, 450, 620, 150, 'The checker fails when\n- an Accepted/Implemented criterion has no tagged test\n- a tag names a criterion no spec defines (typo, deleted AC)\n- the PR body has no Spec: line\nDraft specs only warn. IDs are never reused or reordered.', { color: 'yellow', size: 14 });
  d.text(0, 450, 'Spec status:', { size: 14, color: '#495057' });
  d.text(0, 475, 'Draft -> Accepted -> Implemented\n-> Superseded', { size: 14, color: '#495057' });
  done(d);
}
