/**
 * Local development defaults. Replaced at build time via `fileReplacements` in angular.json for
 * the `dev` and `production` configurations. Never put secrets here: this file ships to browsers.
 */
export const environment = {
  name: 'local',
  /** Base URL of the Balboa API. Local backend (Aspire / `dotnet run`) by default. */
  apiBaseUrl: 'http://localhost:5080',
};
