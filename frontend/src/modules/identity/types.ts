/**
 * Identity module read/write contracts (cards [E7] #105/#107).
 *
 * Mirrors the SISLAB Identity backend DTOs (Contracts/Authorization + Contracts/Administration +
 * Contracts/Invitations). Kept flat and primitive — the UI never sees Lumen or aggregate shapes.
 */

/** A Lumen authorization profile as listed by the "Profiles" tab. */
export interface ProfileDto {
  id: string;
  name: string;
  description: string;
  isSystem: boolean;
}

/** A single permission checkbox, flagged `selected` when the edited profile already grants it. */
export interface PermissionOptionDto {
  id: string;
  code: string;
  displayName: string;
  selected: boolean;
}

/** A group of permission checkboxes (a section heading) in the profile editor. */
export interface PermissionGroupDto {
  groupId: string | null;
  groupName: string;
  permissions: PermissionOptionDto[];
}

/** A profile assigned to a member, shown as a chip in the "Members" tab. */
export interface MemberProfileSummary {
  profileId: string;
  profileName: string;
  isSystem: boolean;
}

/** A company member enriched with identity + assigned profiles (GET /members/enriched). */
export interface EnrichedMemberDto {
  membershipId: string;
  userId: string;
  username: string;
  email: string;
  assignedProfiles: MemberProfileSummary[];
}

/** Request body for inviting a member by e-mail with a profile to grant on accept. */
export interface InviteMemberRequest {
  email: string;
  profileId: string;
}

/** Result of an invite: the invitation id and whether it was a resend of a pending one. */
export interface InviteMemberResult {
  invitationId: string;
  resent: boolean;
}

/** Request body for creating a profile. */
export interface CreateProfileRequest {
  name: string;
  description: string;
}

/** Request body for renaming/re-describing a profile. */
export interface UpdateProfileRequest {
  name: string;
  description: string;
}

/** Anonymous preview of a member invitation (GET /api/companies/invitations/{token}). */
export interface InvitationPreviewDto {
  email: string;
  companyName: string;
  profileName: string;
  /** ISO datetime (UTC) when the invitation stops being acceptable. */
  expiresAt: string;
  /** Lifecycle status label: "Pending" | "Accepted" | "Revoked" | "Expired". */
  status: string;
  /** True only when the invitation can still be accepted right now. */
  acceptable: boolean;
  /** True when the accept form must collect a username/password (no account yet). */
  requiresAccountCreation: boolean;
}

/** Request body for accepting an invitation. Username/password only for new accounts. */
export interface AcceptInvitationRequest {
  token: string;
  username?: string;
  password?: string;
}

/** Result of accepting an invitation. */
export interface AcceptInvitationResult {
  companyId: string;
  userId: string;
  accountCreated: boolean;
}
