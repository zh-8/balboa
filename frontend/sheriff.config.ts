import { noDependencies, sameTag, SheriffConfig } from '@softarc/sheriff-core';

/**
 * Frontend module boundaries (enforced by `npm run lint:boundaries` and the ESLint plugin).
 *
 *   features/<name>  -> may use core, shared and itself; never another feature
 *   core             -> may use shared; never features
 *   shared           -> depends on nothing outside itself (no core, no features)
 *   app root         -> composition root, may use everything
 */
export const config: SheriffConfig = {
  version: 1,
  entryPoints: { app: 'src/main.ts' },
  enableBarrelLess: true,
  excludeRoot: false,
  modules: {
    'src/app/core': ['layer:core'],
    'src/app/shared': ['layer:shared'],
    'src/app/features/<feature>': ['layer:feature', 'feature:<feature>'],
  },
  depRules: {
    root: ['layer:core', 'layer:shared', 'layer:feature'],
    'layer:shared': noDependencies,
    'layer:core': ['layer:shared'],
    'layer:feature': [sameTag, 'layer:core', 'layer:shared'],
    'feature:*': [sameTag, 'layer:core', 'layer:shared'],
  },
};
