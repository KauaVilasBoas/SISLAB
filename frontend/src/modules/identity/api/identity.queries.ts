import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  CreateProfileRequest,
  EnrichedMemberDto,
  InviteMemberRequest,
  InviteMemberResult,
  PermissionGroupDto,
  ProfileDto,
  UpdateProfileRequest,
} from '@/modules/identity/types';

/**
 * Identity module query keys, namespaced under 'identity' so mutations can invalidate the
 * members/profiles caches without touching other modules.
 */
export const identityKeys = {
  all: ['identity'] as const,
  members: () => [...identityKeys.all, 'members'] as const,
  profiles: () => [...identityKeys.all, 'profiles'] as const,
  permissions: (profileId?: string) =>
    [...identityKeys.all, 'permissions', profileId ?? 'new'] as const,
};

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/** Enriched members of the active company (name, e-mail, assigned profile chips). */
export function useMembers() {
  return useQuery({
    queryKey: identityKeys.members(),
    queryFn: () => api.get<EnrichedMemberDto[]>(Endpoints.identity.members.enriched),
    staleTime: 2 * 60_000,
  });
}

/** Every active authorization profile (the "Profiles" tab). */
export function useProfiles() {
  return useQuery({
    queryKey: identityKeys.profiles(),
    queryFn: () => api.get<ProfileDto[]>(Endpoints.identity.profiles.root),
    staleTime: 5 * 60_000,
  });
}

/**
 * The permission catalogue grouped for the checkboxes. When `profileId` is provided (editing),
 * each permission the profile already grants comes back `selected`. Enabled only when the editor
 * is open (`enabled`), so the catalogue isn't fetched until needed.
 */
export function useAvailablePermissions(profileId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: identityKeys.permissions(profileId),
    queryFn: () =>
      api.get<PermissionGroupDto[]>(Endpoints.identity.profiles.availablePermissions, {
        profileId,
      }),
    enabled,
    staleTime: 5 * 60_000,
  });
}

// ---------------------------------------------------------------------------
// Mutations
// ---------------------------------------------------------------------------

/** Invites a member by e-mail with a profile to grant on accept; refreshes the members list. */
export function useInviteMember() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: InviteMemberRequest) =>
      api.post<InviteMemberResult>(Endpoints.identity.members.invite, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: identityKeys.members() }),
  });
}

/** Creates a profile; refreshes the profiles list. */
export function useCreateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateProfileRequest) =>
      api.post<string>(Endpoints.identity.profiles.root, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: identityKeys.profiles() }),
  });
}

/** Renames/re-describes a profile; refreshes the profiles list. */
export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      profileId,
      body,
    }: {
      profileId: string;
      body: UpdateProfileRequest;
    }) => api.put<void>(Endpoints.identity.profiles.byId(profileId), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: identityKeys.profiles() }),
  });
}

/** Reconciles a profile's permissions to the given set; refreshes that profile's permission cache. */
export function useSetPermissions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      profileId,
      permissionIds,
    }: {
      profileId: string;
      permissionIds: string[];
    }) =>
      api.put<void>(Endpoints.identity.profiles.permissions(profileId), {
        permissionIds,
      }),
    onSuccess: (_data, { profileId }) =>
      queryClient.invalidateQueries({ queryKey: identityKeys.permissions(profileId) }),
  });
}

/** Assigns a profile to a member (company-scoped); refreshes the members list. */
export function useAssignProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, profileId }: { userId: string; profileId: string }) =>
      api.post<void>(Endpoints.identity.members.profiles(userId), { profileId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: identityKeys.members() }),
  });
}

/** Removes a profile assignment from a member; refreshes the members list. */
export function useRemoveProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, profileId }: { userId: string; profileId: string }) =>
      api.del<void>(Endpoints.identity.members.profile(userId, profileId)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: identityKeys.members() }),
  });
}
