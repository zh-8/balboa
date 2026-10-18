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
        lint-docs fmt fmt-check run migrate diagrams diagrams-check spec-check \
        infra-lint what-if coverage clean ci

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

test-unit: ## Backend unit tests (Domain + Application)
	dotnet test $(SLN) --no-restore -c Release \
		--filter "Category!=Integration" \
		-- --report-trx

test-arch: ## Architecture tests (module boundaries, layering)
	dotnet test $(BACKEND)/tests/Balboa.ArchitectureTests -c Release

test-int: ## Integration + API tests against real Postgres (requires Docker)
	@docker info >/dev/null 2>&1 || { echo "Docker is not running; test-int needs it."; exit 1; }
	dotnet test $(SLN) --no-restore -c Release --filter "Category=Integration"

test-fe: ## Frontend unit tests (Vitest)
	cd $(FRONTEND) && npm run test -- --run

coverage: ## Backend coverage report, enforces the Domain/Application gate
	dotnet test $(SLN) -c Release --filter "Category!=Integration" \
		--collect "XPlat Code Coverage" --results-directory coverage
	dotnet tool run reportgenerator -- \
		-reports:"coverage/**/coverage.cobertura.xml" \
		-targetdir:coverage/report \
		-reporttypes:"Html;TextSummary;MarkdownSummaryGithub"
	@cat coverage/report/Summary.txt

# ---------------------------------------------------------------------------
# Lint / format
# ---------------------------------------------------------------------------

lint: lint-be lint-fe lint-docs ## Lint everything

lint-be: ## Backend: analyzers as errors (build is the linter)
	dotnet build $(SLN) --no-restore -c Release -warnaserror

lint-fe: ## Frontend: ESLint + Sheriff module boundaries
	cd $(FRONTEND) && npm run lint && npm run lint:boundaries

lint-docs: ## Markdown lint + link check
	cd tools/spec-check && npm run lint:docs

fmt: ## Format backend and frontend in place
	dotnet format $(SLN)
	cd $(FRONTEND) && npm run format

fmt-check: ## Verify formatting without writing (CI uses this)
	dotnet format $(SLN) --verify-no-changes
	cd $(FRONTEND) && npm run format:check

# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------

run: ## Run the whole stack locally via Aspire (API + Angular + Postgres container)
	dotnet run --project $(BACKEND)/src/Balboa.AppHost

migrate: ## Apply EF migrations to the local database
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
	find . -type d -name bin -o -type d -name obj | xargs -r rm -rf
