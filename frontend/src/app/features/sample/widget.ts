// PLACEHOLDER: this feature mirrors the backend Sample module. It exists only to demonstrate the
// pattern (lazy-loaded feature, signals, core ApiClient) and will be replaced by the first real
// bounded context. Do not build on it.

/** Mirrors `WidgetDto(Guid Id, string Sku, string Name, bool IsPublished)` in Sample.Contracts. */
export interface Widget {
  id: string;
  sku: string;
  name: string;
  isPublished: boolean;
}
