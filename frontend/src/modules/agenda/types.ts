/**
 * Agenda module contracts (cards [E10] #69/#70/#71): room bookings, biotério assignments and
 * presentations. Mirrors the backend read DTOs.
 */

// ---------------------------------------------------------------------------
// Rooms & Bookings (#69)
// ---------------------------------------------------------------------------

export type RoomType = 'Lab' | 'OperatingRoom' | 'Vivarium' | 'Office' | 'Meeting';
export type AgendaActivity =
  | 'VonFrey' | 'Hargreaves' | 'TailFlick' | 'RotaRod' | 'Hemogram'
  | 'Dissection' | 'Surgery' | 'AnimalCare' | 'Meeting' | 'Other';

export interface RoomListItem {
  id: string;
  name: string;
  capacity: number;
  type: RoomType;
}

export interface BookingListItem {
  bookingId: string;
  roomId: string;
  roomName: string;
  bookedByName: string;
  activity: AgendaActivity;
  date: string;          // DateOnly: 'YYYY-MM-DD'
  startTime: string;     // TimeOnly: 'HH:mm:ss'
  endTime: string;
  notes: string | null;
  hasConflictWarning: boolean;
}

export interface CreateBookingResponse {
  bookingId: string;
  conflictWarning: boolean;
}

// ---------------------------------------------------------------------------
// Biotério (#70)
// ---------------------------------------------------------------------------

export type AssignmentStatus = 'Pending' | 'Done' | 'Swapped';

export interface BioteriumAssignmentItem {
  id: string;
  assignmentDate: string;  // 'YYYY-MM-DD'
  responsibleName: string;
  status: AssignmentStatus;
  swappedFromName: string | null;
  swapReason: string | null;
  notes: string | null;
}

// ---------------------------------------------------------------------------
// Presentations (#71)
// ---------------------------------------------------------------------------

export type PresentationType = 'Article' | 'Lecture' | 'Preview' | 'Editorial' | 'DolAlert';
export type PresentationStatus = 'Scheduled' | 'Done' | 'Cancelled';

export interface PresentationListItem {
  id: string;
  type: PresentationType;
  title: string;
  doi: string | null;
  presenterName: string;
  scheduledDate: string;  // 'YYYY-MM-DD'
  status: PresentationStatus;
  reminderSent: boolean;
  notes: string | null;
}

// ---------------------------------------------------------------------------
// Improved calendar — unified AgendaEntry model (cards [E10.3]/[E10.4]/[E10.5-7])
// ---------------------------------------------------------------------------

/** Kind of activity an agenda entry represents — drives its colour and the type filter. */
export type AgendaActivityType =
  | 'RoomBooking'
  | 'Experiment'
  | 'Bioterium'
  | 'Presentation'
  | 'Other';

/** How editing a recurring entry should scope (Google-Calendar semantics). */
export type EditScope = 'OnlyThis' | 'ThisAndFollowing' | 'AllOccurrences';

/** A single materialised calendar occurrence returned by GET /api/agenda/calendar. */
export interface CalendarItem {
  id: string;
  title: string;
  activityType: AgendaActivityType;
  experimentId: string | null;
  experimentName: string | null;
  roomId: string | null;
  startDateUtc: string;   // ISO 8601 UTC
  endDateUtc: string;     // ISO 8601 UTC
  isAllDay: boolean;
  isRecurring: boolean;
  recurrenceRule: string | null; // raw RFC 5545 RRULE, null for a one-off — pre-populates the edit form
  occurrenceDate: string; // 'YYYY-MM-DD' — the occurrence's EXDATE key
  responsibleId: string;
  color: string | null; // per-entry '#rrggbb' override, or null to use the automatic activity-type colour
}

/** Advisory scheduling warnings a create/update may return (never blocks the write). */
export type AgendaConflictWarning = 'conflict_person' | 'conflict_room';

/** Result of creating/updating an entry: the id to display plus advisory conflict warnings. */
export interface AgendaEntryMutationResult {
  entryId: string;
  warnings: AgendaConflictWarning[];
}

/** Body of POST /api/agenda/entries. `recurrenceRule` is an RFC 5545 RRULE or null for a one-off. */
export interface CreateAgendaEntryRequest {
  title: string;
  description: string | null;
  startDateUtc: string;
  endDateUtc: string;
  isAllDay: boolean;
  activityType: AgendaActivityType;
  experimentId: string | null;
  roomId: string | null;
  recurrenceRule: string | null;
  color: string | null;
}

/** Body of PUT /api/agenda/entries/{id}. Carries the Google-Calendar edit scope. */
export interface UpdateAgendaEntryRequest {
  editScope: EditScope;
  occurrenceDate: string | null;
  title: string;
  description: string | null;
  startDateUtc: string;
  endDateUtc: string;
  isAllDay: boolean;
  activityType: AgendaActivityType;
  experimentId: string | null;
  roomId: string | null;
  recurrenceRule: string | null;
  color: string | null;
}

/** A single occupied slot on the room-occupancy Gantt (card [E10.11]). */
export interface RoomOccupancySlot {
  roomId: string | null;
  roomName: string | null;
  entryId: string;
  title: string;
  startUtc: string;
  endUtc: string;
  responsibleId: string;
  responsibleName: string;
  color: string | null; // per-entry '#rrggbb' override, or null to use the lane (per-room) colour
}
