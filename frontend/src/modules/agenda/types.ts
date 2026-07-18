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
