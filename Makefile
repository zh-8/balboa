# Balboa task runner.
#
# Deliberately a Makefile: nothing to install, and it is the single contract shared by
# humans, Claude agents and CI. Every CI job calls one of these targets, so "works in CI"
# and "works locally" cannot drift.
#
# Run `make` or `make help` for the list.

.DEFAULT_GOAL := help
SHELL := /bin/bash
.SHELLFLAGS := -eu -o pipefail -c

BACKEND  := backend
FRONTEND := frontend
INFRA    := infra
SLN      := $(BACKEND)/Balboa.slnx
ENV      ?= dev

# Treat these as commands, not files.
.PHONY: help setup build test test-unit test-arch test-int test-fe lint lint-be lint-fe \
        lint-docs fmt fmt-check run run-fe db db-stop migrate diagrams diagrams-check \
        spec-check infra-lint what-if coverage clean ci

help: ## Show this help
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk 'BEGIN{FS=":.*?## "}{printf "  \033[1m%-16s\033[0m %s\n", $$1, $$2}'

# ---------------------------------------------------------------------------
# Setup
# ---------------------------------------------------------------------------

setup: ## Restore all dependencies (backend, frontend, tools)
	dotnet restore $(SLN)
	cd $(FRONTEND) && npm ci
	cd tools/spec-check && npm ci
	cd tools/diagrams && npm ci

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------

build: ## Build backend and frontend
	dotnet build $(SLN) --no-restore -c Release
	cd $(FRONTEND) && npm run build

# ---------------------------------------------------------------------------
# Test
#   test      -> fast gate, no Docker required
#   test-int  -> needs Docker (Testcontainers spins up Postgres)
# ---------------------------------------------------------------------------

test: test-unit test-arch test-fe ## Fast tests: unit + architecture + frontend (no Docker)

# xUnit v3 runs on Microsoft.Testing.Platform (see the "test" block in
# global.json), so the CLI surface differs from VSTest:
#   --solution, not a bare path
#   --filter-trait / --filter-not-trait, not --filter "Category=..."
#   --ignore-exit-code 8 because exit code 8 means "no tests ran", which is the
#   correct outcome for an assembly that holds only the filtered-out category.
test-unit: ## Backend unit tests (Domain + Application)
	dotnet test --solution $(SLN) -c Release \
		--filter-not-trait "Category=Integration" \
		--ignore-exit-code 8

test-arch: ## Architecture tests (module boundaries, layering)
	dotnet test --project $(BACKEND)/tests/Balboa.ArchitectureTests -c Release

test-int: ## Integration + API tests against real Postgres (requires Docker)
	@docker info >/dev/null 2>&1 || { echo "Docker is not running; test-int needs it."; exit 1; }
	dotnet test --solution $(SLN) -c Release \
		--filter-trait "Category=Integration" \
		--ignore-exit-code 8

test-fe: ## Frontend unit tests (Vitest)
	cd $(FRONTEND) && npm run test -- --run

# Coverage uses Microsoft.Testing.Extensions.CodeCoverage (the native MTP path), referenced
# only by the unit-test projects. It is passed per project: the other test assemblies do not
# know --coverage and would reject it. Paths must be absolute or the extension crashes.
# backend/codecoverage.settings.xml limits instrumentation to *.Domain and *.Application, and
# tools/coverage-gate/check.mjs fails the target when either is below 85% (ADR-0014), or when
# a Domain/Application project is missing from the reports because no test touched it.
COVERAGE_MIN ?= 85

coverage: ## Backend coverage; fails if Domain or Application is below the 85% line gate
	rm -rf coverage
	for p in $$(find $(BACKEND)/tests -name '*.UnitTests.csproj'); do \
		dotnet test --project $$p -c Release \
			--coverage --coverage-output-format cobertura \
			--coverage-settings $(CURDIR)/$(BACKEND)/codecoverage.settings.xml \
			--results-directory $(CURDIR)/coverage || exit 1; \
	done
	node tools/coverage-gate/check.mjs --min $(COVERAGE_MIN) --expect-from $(BACKEND)/src $$(find coverage -name '*.cobertura.xml')

# ---------------------------------------------------------------------------
# Lint / format
# ---------------------------------------------------------------------------

lint: lint-be lint-fe lint-docs ## Lint everything

lint-be: ## Backend: analyzers as errors (build is the linter)
	dotnet build $(SLN) --no-restore -c Release -warnaserror

lint-fe: ## Frontend: ESLint + Sheriff module boundaries
	cd $(FRONTEND) && npm run lint && npm run lint:boundaries

lint-docs: ## Markdown lint, plus the link check when lychee is installed
	cd tools/spec-check && npm run lint:docs
	@# The link check ran only in CI, which is how a lychee.toml that could not even be
	@# parsed survived: the docs job had never passed. lychee is a single binary and not an
	@# npm dependency, so it is used when present rather than required.
	@if command -v lychee >/dev/null 2>&1; then \
		lychee --config lychee.toml --no-progress './**/*.md'; \
	else \
		echo "note: lychee not installed, so the link check was skipped here; CI always runs it."; \
	fi

fmt: ## Format backend and frontend in place
	dotnet format $(SLN)
	cd $(FRONTEND) && npm run format

fmt-check: ## Verify formatting without writing (CI uses this)
	dotnet format $(SLN) --verify-no-changes
	cd $(FRONTEND) && npm run format:check

# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------

# Aspire was the intended orchestrator, but its restore pulls several large
# packages (Aspire.Hosting.Orchestration, Grpc.Tools, KubernetesClient) that
# repeatedly timed out on this network. docker compose does the same job here
# with no restore cost. See ADR-0019.
run: db ## Run the API locally against a Postgres container
	dotnet run --project $(BACKEND)/src/Balboa.Host -- migrate
	dotnet run --project $(BACKEND)/src/Balboa.Host

run-fe: ## Run the Angular dev server (expects `make run` in another terminal)
	cd $(FRONTEND) && npm start

# Balboa's local Postgres is on 55432, NOT the default 5432 — a developer machine usually
# already has something on 5432, and the dev connection string is only "localhost:<port>",
# so a collision would quietly aim Balboa's migrations at an unrelated database rather than
# fail. Keep this in step with backend/docker-compose.yml and appsettings.Development.json.
BALBOA_PG_PORT ?= 55432
export BALBOA_PG_PORT

db: ## Start the local Postgres container and wait for it to be healthy
	@# Refuse to continue if a stranger holds the port: `migrate` would target their database.
	@# Our own container holding it is fine, because `up -d --wait` is idempotent — so compare
	@# the listener against the port our container actually publishes, not merely whether a
	@# container exists (it may be running on a different port than the one asked for).
	@if (exec 3<>/dev/tcp/127.0.0.1/$(BALBOA_PG_PORT)) 2>/dev/null; then \
		mine=$$(docker compose -f $(BACKEND)/docker-compose.yml port postgres 5432 2>/dev/null \
			| sed 's/.*://' || true); \
		if [ "$$mine" != "$(BALBOA_PG_PORT)" ]; then \
			echo "Port $(BALBOA_PG_PORT) is already in use, and not by Balboa's container."; \
			echo "Stop whatever holds it, or run:  make db BALBOA_PG_PORT=<free-port>"; \
			echo "and point the Host at the same port with ConnectionStrings__balboa."; \
			exit 1; \
		fi; \
	fi
	docker compose -f $(BACKEND)/docker-compose.yml up -d --wait

db-stop: ## Stop the local Postgres container
	docker compose -f $(BACKEND)/docker-compose.yml down

migrate: db ## Apply EF migrations to the local database
	dotnet run --project $(BACKEND)/src/Balboa.Host -- migrate

# ---------------------------------------------------------------------------
# Docs and diagrams
# ---------------------------------------------------------------------------

diagrams: ## Export every .excalidraw to SVG
	cd tools/diagrams && npm run export

diagrams-check: ## Fail if an exported SVG is stale relative to its source
	cd tools/diagrams && npm run check

spec-check: ## Verify every acceptance criterion has a tagged test
	cd tools/spec-check && npm run check

# ---------------------------------------------------------------------------
# Infrastructure
# ---------------------------------------------------------------------------

infra-lint: ## Build and lint all Bicep (no Azure login needed)
	az bicep build --file $(INFRA)/bicep/main.bicep --stdout >/dev/null
	az bicep build --file $(INFRA)/bootstrap/bootstrap.bicep --stdout >/dev/null
	az bicep lint --file $(INFRA)/bicep/main.bicep
	az bicep lint --file $(INFRA)/bootstrap/bootstrap.bicep

what-if: ## Preview infra changes for ENV (default dev). Read-only.
	./$(INFRA)/scripts/what-if.sh $(ENV)

# ---------------------------------------------------------------------------
# Aggregates
# ---------------------------------------------------------------------------

ci: fmt-check lint test diagrams-check spec-check infra-lint ## Everything CI runs except integration tests

clean: ## Remove build output
	dotnet clean $(SLN) || true
	rm -rf $(FRONTEND)/dist $(FRONTEND)/.angular coverage artifacts
	@# MUST prune node_modules and .git: plenty of npm packages ship their entry points in a
	@# bin/ directory, so an unpruned sweep uninstalls the frontend toolchain (eslint, prettier,
	@# ng) and leaves a working tree that only `npm ci` can repair. -print0/-0 for paths with
	@# spaces; the parenthesised -o group keeps the prune applying to both names.
	find . -type d \( -name node_modules -o -name .git \) -prune -o \
	       -type d \( -name bin -o -name obj \) -print0 | xargs -0 -r rm -rf
