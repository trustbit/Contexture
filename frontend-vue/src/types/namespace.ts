import { NamespaceTemplateId } from "~/types/namespace-templates";

export type NamespaceId = string;
export type NamespaceLabelId = string;

export interface CreateNamespace {
  name: NamespaceId;
  labels: CreateNamespaceLabel[];
  template?: NamespaceTemplateId;
}

export interface CreateNamespaceLabel {
  name: string;
  value?: string;
  template?: string;
}

export interface Namespace {
  id: string;
  template: string;
  name: string;
  labels: NamespaceLabel[];
}

export interface NamespaceLabel {
  id: NamespaceLabelId;
  name: string;
  value: string;
  template: string;
}
