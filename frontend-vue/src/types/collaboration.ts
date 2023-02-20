import { BoundedContextId } from "~/types/boundedContext";
import { DomainId } from "~/types/domain";

export type CollaborationId = string;
export type CollaboratorKeys = keyof Collaborator;
export type RelationshipTypes = keyof RelationshipType & "unknown";

export interface Collaboration {
  id: CollaborationId;
  description?: string;
  initiator: Collaborator;
  recipient: Collaborator;
  relationshipType: RelationshipType | "unknown";
}

export interface CreateCollaborator {
  description?: string;
  initiator: Collaborator;
  recipient: Collaborator;
}

export interface RelationshipType {
  symmetric?: SymmetricRelationship;
  upstreamDownstream?: {
    role?: InitiatorCustomerSupplierRole;
    downstreamType?: DownstreamRelationship;
    upstreamType?: UpstreamRelationship;
    initiatorRole?: InitiatorRole;
  };
}

export enum InitiatorRole {
  Upstream = "Upstream",
  Downstream = "Downstream",
}

export enum InitiatorCustomerSupplierRole {
  Supplier = "Supplier",
  Customer = "Customer",
}

export enum SymmetricRelationship {
  SharedKernel = "SharedKernel",
  Partnership = "Partnership",
  SeparateWays = "SeparateWays",
  BigBallOfMud = "BigBallOfMud",
}

export enum UpstreamRelationship {
  Upstream = "Upstream",
  PublishedLanguage = "PublishedLanguage",
  OpenHost = "OpenHost",
}

export enum DownstreamRelationship {
  Downstream = "Downstream",
  AntiCorruptionLayer = "AntiCorruptionLayer",
  Conformist = "Conformist",
}

export interface Collaborator {
  boundedContext?: BoundedContextId;
  domain?: DomainId;
  externalSystem?: string;
  frontend?: string;
}

export interface RelationshipBetweenCollaborators {
  value: DownstreamRelationship | RelationshipType | UpstreamRelationship;
  label: string;
  description: string;
}
