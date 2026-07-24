import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, httpClient } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  AttachEvidenceFields,
  AttachmentListItem,
  AttachmentTargetKind,
} from '@/modules/in-vivo/types';

/**
 * Evidence-attachment query keys (SISLAB-09), namespaced so an upload invalidates only the affected animal's
 * gallery (optionally narrowed to a single reading/analysis target).
 */
export const attachmentKeys = {
  all: ['in-vivo', 'attachments'] as const,
  list: (animalId: string, targetKind: string | null, targetId: string | null) =>
    [...attachmentKeys.all, animalId, targetKind ?? null, targetId ?? null] as const,
};

export interface ListAttachmentsParams {
  animalId: string;
  targetKind?: AttachmentTargetKind;
  targetId?: string;
}

/** An animal's evidence attachments (newest first), optionally narrowed to one reading/analysis. */
export function useAttachments(params: ListAttachmentsParams, enabled = true) {
  return useQuery({
    queryKey: attachmentKeys.list(
      params.animalId,
      params.targetKind ?? null,
      params.targetId ?? null,
    ),
    queryFn: () =>
      api.get<AttachmentListItem[]>(Endpoints.attachments.root, {
        animalId: params.animalId,
        targetKind: params.targetKind,
        targetId: params.targetId,
      }),
    enabled: enabled && Boolean(params.animalId),
  });
}

/** The multipart payload to attach evidence: the metadata fields plus the file itself. */
export interface AttachEvidenceInput extends AttachEvidenceFields {
  file: File;
}

/**
 * Uploads an evidence file (photo/PDF) to an animal's reading/analysis via multipart/form-data. Axios detects the
 * FormData body and sets the multipart boundary itself. Invalidates the affected animal's gallery.
 */
export function useAttachEvidence() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AttachEvidenceInput) => {
      const form = new FormData();
      form.append('animalId', input.animalId);
      form.append('targetKind', input.targetKind);
      form.append('ownerId', input.ownerId);
      form.append('targetId', input.targetId);
      if (input.origin) form.append('origin', input.origin);
      form.append('file', input.file);
      return api.post<string>(Endpoints.attachments.root, form);
    },
    onSuccess: (_id, input) => {
      // Refresh every gallery of the animal (both the target-narrowed and the animal-wide caches).
      void queryClient.invalidateQueries({ queryKey: attachmentKeys.all });
      void queryClient.invalidateQueries({
        queryKey: attachmentKeys.list(input.animalId, null, null),
      });
    },
  });
}

/**
 * Streams an attachment's bytes and hands the browser a download of the original file name. Uses the raw client
 * with `responseType: 'blob'` (the typed `api` helpers only unwrap JSON envelopes).
 */
export async function downloadAttachment(
  attachmentId: string,
  fileName: string,
): Promise<void> {
  const response = await httpClient.get(Endpoints.attachments.content(attachmentId), {
    responseType: 'blob',
  });

  const url = URL.createObjectURL(response.data as Blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}
