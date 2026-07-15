import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  AcceptInvitationRequest,
  AcceptInvitationResult,
  InvitationPreviewDto,
} from '@/modules/identity/types';

/**
 * Public (anonymous) invitation flow query keys — namespaced under 'invitation' and keyed by the
 * raw token from the e-mail link. These run outside RequireAuth (no session yet).
 */
export const invitationKeys = {
  all: ['invitation'] as const,
  preview: (token: string) => [...invitationKeys.all, 'preview', token] as const,
};

/** Loads the anonymous invitation preview (company, e-mail, profile, expiry, acceptability). */
export function useInvitationPreview(token: string) {
  return useQuery({
    queryKey: invitationKeys.preview(token),
    queryFn: () =>
      api.get<InvitationPreviewDto>(Endpoints.identity.invitations.preview(token)),
    enabled: token.length > 0,
    retry: false,
  });
}

/** Accepts an invitation — links an existing account or creates one from username/password. */
export function useAcceptInvitation() {
  return useMutation({
    mutationFn: (body: AcceptInvitationRequest) =>
      api.post<AcceptInvitationResult>(Endpoints.identity.invitations.accept, body),
  });
}
