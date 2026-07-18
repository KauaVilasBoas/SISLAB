/**
 * Authoritative catalogue of permission codes the SPA gates on (card [E7] #110/#111 rollout).
 *
 * Every string here MUST exactly match a permission row seeded on the backend (project
 * `SISLAB.Migrations`, `Seed*Permission(s)` migrations) — a typo silently breaks the gate.
 * Codes follow Lumen's runtime `<Controller>.<Action>` convention (controller name without the
 * `Controller` suffix + action name), except a few explicit feature-level codes (e.g.
 * `Inventory.Cost.Read`). The backend `[RequirePermission]` remains the authority; this catalogue
 * only drives which permission-gated UI to render.
 *
 * Grouped by module so the map "UI element → endpoint → permission code" stays reviewable.
 */
export const Permissions = {
  /** Estoque — StockController write endpoints + the movements ledger read. */
  stock: {
    registerItem: 'Stock.RegisterStockItem',
    updateItem: 'Stock.UpdateStockItem',
    registerEntry: 'Stock.RegisterEntry',
    registerConsumption: 'Stock.RegisterConsumption',
    transfer: 'Stock.Transfer',
    dispose: 'Stock.Dispose',
    registerCount: 'Stock.RegisterCount',
    listMovements: 'Stock.ListStockMovements',
  },
  /** Estoque — StorageLocationsController write endpoints (management screen). */
  storageLocations: {
    register: 'StorageLocations.Register',
    update: 'StorageLocations.Update',
    changeStatus: 'StorageLocations.ChangeStatus',
  },
  /** Estoque — cost reports (gestão-sensitive), shared by both cost endpoints. */
  inventory: {
    costRead: 'Inventory.Cost.Read',
  },
  /** Estoque — EquipmentController write endpoints. */
  equipment: {
    register: 'Equipment.Register',
    update: 'Equipment.Update',
    changeStatus: 'Equipment.ChangeStatus',
    defineCalibration: 'Equipment.DefineCalibration',
    recordMaintenance: 'Equipment.RecordMaintenance',
  },
  /** Estoque — PartnersController write endpoints. */
  partners: {
    register: 'Partners.Register',
    update: 'Partners.Update',
    deactivate: 'Partners.Deactivate',
    reactivate: 'Partners.Reactivate',
    recordSample: 'Partners.RecordSample',
    removeSample: 'Partners.RemoveSample',
  },
  /** Notificações — NotificationsController write endpoints. */
  notifications: {
    markAsRead: 'Notifications.MarkAsRead',
    readAll: 'Notifications.ReadAll',
  },
  /** Configuração — per-tenant reference data write endpoints. */
  configuration: {
    createUnit: 'Unit.Create',
    createRoom: 'Room.Create',
    createReferenceRange: 'ReferenceRange.Create',
    createItemCategory: 'ItemCategory.Create',
    setExpiryWarningWindow: 'ExpiryPolicy.SetWarningWindow',
  },
  /** Perfis e Permissões — ProfilesController + MemberProfilesController. */
  profiles: {
    listAvailablePermissions: 'Profiles.ListAvailablePermissions',
    listProfiles: 'Profiles.ListProfiles',
    createProfile: 'Profiles.CreateProfile',
    updateProfile: 'Profiles.UpdateProfile',
    setProfilePermissions: 'Profiles.SetProfilePermissions',
    assignProfile: 'MemberProfiles.AssignProfile',
    removeProfile: 'MemberProfiles.RemoveProfile',
  },
  /** Membros da Empresa — CompanyMembersController + invitations. */
  members: {
    list: 'CompanyMembers.ListMembers',
    listEnriched: 'CompanyMembers.ListEnrichedMembers',
    checkRemovalEligibility: 'CompanyMembers.CheckRemovalEligibility',
    invite: 'CompanyMembers.InviteMember',
  },
  /** Experimentos — delineamento in vivo (ProjectsController write endpoints). */
  projects: {
    create: 'Projects.Create',
    addBatch: 'Projects.AddBatch',
    addGroup: 'Projects.AddGroup',
    addAnimal: 'Projects.AddAnimal',
    startBatch: 'Projects.StartBatch',
  },
  /** Experimentos — testes comportamentais in vivo (ExperimentsController behavioural write endpoints). */
  experiments: {
    createBehavioral: 'Experiments.CreateBehavioral',
    recordTimepoint: 'Experiments.RecordTimepoint',
    calculateBehavioral: 'Experiments.CalculateBehavioral',
  },
  /** Experimentos — biobanco (SamplesController write endpoints). */
  samples: {
    collect: 'Samples.Collect',
    analyse: 'Samples.Analyse',
    recordResult: 'Samples.RecordResult',
  },
  /** Agenda — salas e reservas (#69), biotério (#70), apresentações (#71). */
  agenda: {
    register: 'Rooms.Register',
    createBooking: 'Rooms.CreateBooking',
    cancelBooking: 'Rooms.CancelBooking',
    generateWeek: 'Bioterium.Generate',
    swap: 'Bioterium.Swap',
    markDone: 'Bioterium.MarkDone',
    schedule: 'Presentations.Schedule',
    reschedule: 'Presentations.Reschedule',
    cancelPresentation: 'Presentations.Cancel',
  },
  // Auditoria — a trilha (AuditController.List/Export) é apenas [Authorize], sem [RequirePermission];
  // qualquer membro autenticado da empresa lê, então não há código a gatear aqui (guardado pelo drift test).
} as const;
