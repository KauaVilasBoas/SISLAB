/**
 * Equipment module read/write contracts (card [E7] #48).
 *
 * Mirrors the SISLAB Inventory backend read models (EquipmentListItem, EquipmentDetail) and the
 * write request bodies exposed by EquipmentController. Enums cross the wire as their NAME (the API
 * serializes them with JsonStringEnumConverter), so they are modelled as string literal unions.
 * Kept flat and primitive — the UI never sees the Equipment aggregate or its value objects.
 */

/** Operational status of a laboratory equipment, mirroring the backend EquipmentStatus enum. */
export type EquipmentStatus = 'InUse' | 'Available' | 'UnderMaintenance' | 'Inactive';

/** Derived calibration classification, mirroring the backend CalibrationStatus enum. */
export type CalibrationStatus = 'NotRequired' | 'UpToDate' | 'DueSoon' | 'Overdue';

/** Nature of a maintenance event, mirroring the backend MaintenanceType enum. */
export type MaintenanceType = 'Preventive' | 'Corrective' | 'Calibration';

/** An equipment row as listed by the equipment table (GET /api/inventory/equipment). */
export interface EquipmentListItem {
  id: string;
  name: string;
  assetTag: string;
  manufacturer: string | null;
  model: string | null;
  status: EquipmentStatus;
  storageLocationId: string | null;
  storageLocationName: string | null;
  /** Next planned calibration date, ISO "YYYY-MM-DD", or null when none is scheduled. */
  nextCalibrationDate: string | null;
  calibrationStatus: CalibrationStatus;
  isActive: boolean;
}

/** A single equipment's detail (GET /api/inventory/equipment/{id}). */
export interface EquipmentDetail extends EquipmentListItem {
  /** Last calibration date, ISO "YYYY-MM-DD", or null. */
  lastCalibrationDate: string | null;
  /** Most recent maintenance date, ISO "YYYY-MM-DD", or null. */
  lastMaintenanceDate: string | null;
}

/** Filters applied to the equipment listing; empty values mean "no filter". */
export interface EquipmentFilters {
  status?: CalibrationStatus;
  storageLocationId?: string;
  search?: string;
}

/** Request body for registering a new equipment (POST /api/inventory/equipment). */
export interface RegisterEquipmentRequest {
  name: string;
  assetTag: string;
  brand: string | null;
  model: string | null;
  storageLocationId: string | null;
  status: EquipmentStatus;
  lastCalibration: string | null;
  nextCalibration: string | null;
}

/** Request body for updating an equipment's identification data (PUT /api/inventory/equipment/{id}). */
export interface UpdateEquipmentRequest {
  name: string;
  assetTag: string;
  brand: string | null;
  model: string | null;
  storageLocationId: string | null;
}

/** Request body for moving an equipment to a new status (POST .../status). */
export interface ChangeEquipmentStatusRequest {
  status: EquipmentStatus;
}

/** Request body for defining/clearing a calibration schedule (PUT .../calibration). */
export interface DefineEquipmentCalibrationRequest {
  lastCalibration: string | null;
  nextCalibration: string | null;
}

/** Request body for logging a maintenance event (POST .../maintenances). */
export interface RecordEquipmentMaintenanceRequest {
  date: string;
  type: MaintenanceType;
  notes: string | null;
}
