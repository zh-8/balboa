# Glossary - ubiquitous language

## Purpose

In DDD the ubiquitous language is the vocabulary shared by domain experts, specs, code and
tests. A term here means exactly one thing, and the code uses the same word.

**Balboa's product domain is not yet defined.** This glossary is therefore nearly empty on
purpose. Domain terms (the nouns and verbs of the business) arrive together with the first
real bounded context, in the same PR as its spec. The Sample module's terms are placeholders
and are deliberately not listed here (see
[SPEC-0001](specs/SPEC-0001-sample-module/spec.md)).

## Rules for adding a term

1. A term enters the glossary in the same PR as the spec or code that first needs it.
2. One entry, one meaning. If a word means different things in two bounded contexts, add one
   entry per context, each prefixed with its context name, instead of a compromise
   definition.
3. Define the term in domain language, not implementation language. Say what it is, not which
   class stores it.
4. Use the glossary spelling in code, tests, API routes and UI copy. A rename is a glossary
   change first.
5. Keep entries alphabetical within each section.
6. Retire a term by marking it `(deprecated)` with a pointer to its replacement; do not
   delete it while old specs still use it.

## Structural terms

These describe the architecture itself and apply now.

| Term | Meaning |
|---|---|
| Aggregate | A cluster of entities and value objects treated as one unit for consistency. Changes go through the aggregate root, and one transaction changes one aggregate. |
| Bounded context | The boundary within which one domain model and its language are consistent. In Balboa, each bounded context is one module. |
| Contracts | A module's public surface (`*.Contracts` project): integration events, query interfaces and DTOs. The only part of a module another module may reference. |
| Domain event | A fact that happened inside a module, named in the past tense. Dispatched in-process before commit and never leaves the module. |
| Inbox | A subscriber-side table recording which integration events were already handled, so redelivery is idempotent. |
| Integration event | A fact published for other modules, defined in `*.Contracts`. Written to the outbox in the same transaction as the change that caused it. |
| Module | A folder of four projects (Contracts, Domain, Application, Infrastructure) implementing one bounded context inside the single deployable. |
| Outbox | A per-module table where integration events are stored in the same transaction as the aggregate change, then delivered after commit. |

## Domain terms

_None yet. They arrive with the first real bounded context._
