import {
  CollaboratorKeys,
  DownstreamRelationship,
  InitiatorCustomerSupplierRole,
  RelationshipBetweenCollaborators,
  SymmetricRelationship,
  UpstreamRelationship,
} from "~/types/collaboration";

export const symmetricOptions: RelationshipBetweenCollaborators[] = [
  {
    value: {
      symmetric: SymmetricRelationship.SharedKernel,
    },
    label: "Shared Kernel (SK)",
    description: "Technical artefacts are shared between the collaborators",
  },
  {
    value: {
      symmetric: SymmetricRelationship.Partnership,
    },
    label: "Partnership (PS)",
    description: "The collaborators work together to reach a common goal",
  },
  {
    value: {
      symmetric: SymmetricRelationship.SeparateWays,
    },
    label: "Separate Ways (SW)",
    description: "The collaborators decided to NOT use information, but rather work in seperate ways",
  },
  {
    value: {
      symmetric: SymmetricRelationship.BigBallOfMud,
    },
    label: "Big Ball of Mud (BBoM)",
    description: "It's complicated...",
  },
];

export const customerSupplierOptions: RelationshipBetweenCollaborators[] = [
  {
    value: {
      upstreamDownstream: {
        role: InitiatorCustomerSupplierRole.Customer,
      },
    },
    label: "Customer (CUS)",
    description: "The collaborator is in the customer role",
  },
  {
    value: {
      upstreamDownstream: {
        role: InitiatorCustomerSupplierRole.Supplier,
      },
    },
    label: "Supplier (SUP)",
    description: "The collaborator is in the supplier role",
  },
];

export const upstreamCollaborator: RelationshipBetweenCollaborators[] = [
  {
    value: UpstreamRelationship.Upstream,
    label: "Upstream (US)",
    description: "The collaborator is just Upstream",
  },
  {
    value: UpstreamRelationship.PublishedLanguage,
    label: "Published Language (PL)",
    description: "The collaborator is using a Published Language",
  },
  {
    value: UpstreamRelationship.OpenHost,
    label: "Open Host Service (OHS)",
    description: "The collaborator is providing an Open Host Service",
  },
];

export const upstreamCollaborationRelationship: RelationshipBetweenCollaborators[] = [
  {
    value: DownstreamRelationship.Downstream,
    label: "Downstream (DS)",
    description: "I'm just Downstream",
  },
  {
    value: DownstreamRelationship.AntiCorruptionLayer,
    label: "Anti-Corruption Layer (ACL)",
    description: "I'm using an Anti-Corruption-Layer to shield me from changes",
  },
  {
    value: DownstreamRelationship.Conformist,
    label: "Conformist (CF)",
    description: "I'm Conformist to upstream changes",
  },
];

export const downstreamCollaborator: RelationshipBetweenCollaborators[] = [
  {
    value: DownstreamRelationship.Downstream,
    label: "Downstream (DS)",
    description: "The collaborator is just Downstream",
  },
  {
    value: DownstreamRelationship.AntiCorruptionLayer,
    label: "Anti-Corruption Layer (ACL)",
    description: "The collaborator is using an Anti-Corruption-Layer to shield from my changes",
  },
  {
    value: DownstreamRelationship.Conformist,
    label: "Conformist (CF)",
    description: "The collaborator is Conformist to my upstream changes",
  },
];

export const downstreamCollaborationRelationship: RelationshipBetweenCollaborators[] = [
  {
    value: UpstreamRelationship.Upstream,
    label: "Upstream (US)",
    description: "I'm just Upstream",
  },
  {
    value: UpstreamRelationship.PublishedLanguage,
    label: "Published Language (PL)",
    description: "I'm using a Published Language",
  },
  {
    value: UpstreamRelationship.OpenHost,
    label: "Open Host Service (OHS)",
    description: "I'm providing an Open Host Service",
  },
];

export const collaboratorOptions: { value: CollaboratorKeys; label: string }[] = [
  {
    value: "boundedContext",
    label: "Bounded Context",
  },
  {
    value: "domain",
    label: "Domain",
  },
  {
    value: "externalSystem",
    label: "External System",
  },
  {
    value: "frontend",
    label: "Frontend",
  },
];
