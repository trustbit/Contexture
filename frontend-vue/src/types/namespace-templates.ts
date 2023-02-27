export type NamespaceTemplateId = string;
export type NamespaceTemplateItemId = string;

export interface NamespaceTemplate {
  id: NamespaceTemplateId;
  name: string;
  description: string;
  template: NamespaceTemplateItem[];
}

export interface NamespaceTemplateItem {
  id: NamespaceTemplateItemId;
  name: string;
  description: string;
  placeholder: string;
}
