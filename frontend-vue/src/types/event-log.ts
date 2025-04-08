type EventType =
  | "ShortNameAssigned"
  | "BoundedContextRenamed"
  | "BoundedContextReclassified"
  | "BoundedContextCreated"
  | "BoundedContextImported"
  | "BoundedContextRemoved"
  | "BoundedContextMovedToDomain"
  | "DescriptionChanged"
  | "BusinessDecisionsUpdated"
  | "UbiquitousLanguageUpdated"
  | "DomainRolesUpdated"
  | "MessagesUpdated"
  | "DomainImported"
  | "DomainCreated"
  | "SubDomainCreated"
  | "DomainRenamed"
  | "CategorizedAsSubdomain"
  | "PromotedToDomain"
  | "VisionRefined"
  | "DomainRemoved"
  | "NamespaceImported"
  | "NamespaceAdded"
  | "NamespaceRemoved"
  | "LabelAdded"
  | "LabelRemoved"
  | "LabelUpdated";

export interface EventLogEntry {
  eventType: EventType;
  timestamp: string;
  eventData: any;
}
