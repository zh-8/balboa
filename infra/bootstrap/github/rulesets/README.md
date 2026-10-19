# GitHub rulesets

Branch protection kept as JSON so it is reviewable, diffable and re-appliable.
`bootstrap.sh` applies `main.json` by name: it looks up the existing ruleset called
`main-protection` and `PUT`s over it, or `POST`s it if absent. Re-running is therefore safe.

To apply a change by hand:

```bash
ID=$(gh api repos/zh-8/balboa/rulesets --jq '.[] | select(.name=="main-protection") | .id')
gh api -X PUT "repos/zh-8/balboa/rulesets/$ID" --input main.json
```

## What `main.json` enforces

| Rule | Effect |
|---|---|
| `pull_request` | No direct pushes to `main`. 0 required approvals (solo maintainer), but review threads must be resolved and stale reviews are dismissed on push |
| `required_status_checks` | `ci-ok` must pass. `strict` means the branch must be up to date with `main` first |
| `required_linear_history` | Squash merges only, so history stays bisectable |
| `deletion` / `non_fast_forward` | `main` cannot be deleted or force-pushed |
| `allowed_merge_methods` | Squash only — merge commits and rebase are off |
| Bypass | Repository admins, in `pull_request` mode only: an emergency still goes through a PR, it just does not wait for the checks |

`ci-ok` is a single aggregate job in `ci.yml` that depends on every other job. One required
check means adding a CI job never needs a ruleset change.

## Not enabled: required signed commits

`required_signatures` is deliberately absent. No signing key is configured on this machine
(`git config --global user.signingkey` is unset), so turning it on would reject every commit,
including the ones that would fix it.

To adopt it, configure SSH signing first:

```bash
git config --global gpg.format ssh
git config --global user.signingkey ~/.ssh/id_ed25519.pub
git config --global commit.gpgsign true
# then add the key to GitHub as a SIGNING key (not just an auth key):
gh ssh-key add ~/.ssh/id_ed25519.pub --type signing --title "balboa signing"
```

Verify that a signed commit is accepted, then add `{"type": "required_signatures"}` to the
`rules` array and re-apply.
