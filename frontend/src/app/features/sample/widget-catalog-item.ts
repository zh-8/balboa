// PLACEHOLDER: this feature mirrors the backend Sample module. It exists only to demonstrate the
// pattern (lazy-loaded feature, signals, core ApiClient) and will be replaced by the first real
// bounded context. Do not build on it.

/** A generic catalogue item. Assumed contract of `GET /api/sample/widgets`. */
export interface WidgetCatalogItem {
  id: string;
  name: string;
  description: string | null;
}
