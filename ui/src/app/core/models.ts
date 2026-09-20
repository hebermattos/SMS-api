export type PortalRole = 'admin' | 'tenant';
export type PortalContext = 'platform' | 'tenant';
export type PortalPermissionRole = 'user' | 'administrator';
export interface TokenResponse { access_token: string; refresh_token?: string; token_type: string; expires_in: number; }
export interface PortalSession { token: string; refreshToken?: string; identity: string; role: PortalRole; context: PortalContext; permissionRole: PortalPermissionRole; }
export interface TenantRateLimitSettings { requestsPerMinute: number; smsPerMinute: number; }
export interface Tenant { id: string; name: string; timeZoneId: string; isActive: boolean; createdAt: string; }
export interface Client { id: string; clientId: string; isActive: boolean; createdAt: string; }
export interface Administrator { id: string; username: string; isActive: boolean; createdAt: string; }
export interface PortalUser { id: string; tenantId: string | null; username: string; email: string; context: PortalContext; role: PortalPermissionRole; isActive: boolean; createdAt: string; }
export interface IssuedSecret { clientId: string; clientSecret: string; }
export interface ProvisionedTenant { tenant_id: string; name: string; client_id: string; client_secret: string; }
export interface ProviderField { key: string; label: string; secret: boolean; required: boolean; }
export interface ProviderDefinition { name: string; accountLabel: string; secretLabel: string; fields: ProviderField[]; }
export interface ProviderConfig {
  provider: string; accountId: string; fromNumber: string; isActive: boolean; isDefault: boolean;
  hasApiSecret: boolean; settings: Record<string, string | null>; configuredSecrets: string[];
}
export interface AvailableProvider { name: string; fromNumber: string; isDefault: boolean; }
export interface Overview { name: string; timeZoneId: string; outbound: number; inbound: number; delivered: number; failed: number; pending: number; providers: AvailableProvider[]; }
export interface Message {
  id: string; from: string; to: string; body: string; provider: string; providerMessageId: string | null;
  direction: number; status: number; createdAt: string; scheduledAt: string | null; updatedAt: string | null;
}
export interface StatusHistory { id: string; messageId: string; status: number; createdAt: string; }
export interface LogEntry { id: number; timestamp: string; severity: string; category: string; message: string; traceId: string | null; spanId: string | null; }
export interface SendResult { id: string; provider: string; status: string; scheduledAt: string | null; }
export interface BlockedNumber { id: string; phoneNumber: string; source: string; reason: string | null; createdAt: string; updatedAt: string | null; }
export interface SmsReportProviderSummary { provider: string; totalMessages: number; delivered: number; failed: number; }
export interface SmsReportSummary { totalMessages: number; scheduled: number; queued: number; sent: number; delivered: number; failed: number; received: number; outbound: number; inbound: number; byProvider: SmsReportProviderSummary[]; }
export interface PlatformSmsReportTenantSummary { tenantId: string; tenantName: string; totalMessages: number; scheduled: number; queued: number; sent: number; delivered: number; failed: number; received: number; }
export interface PlatformSmsReportSummary { totalMessages: number; scheduled: number; queued: number; sent: number; delivered: number; failed: number; received: number; byTenant: PlatformSmsReportTenantSummary[]; }

export type AlertRepeatMode = 1 | 2;
export interface AlertRule {
  id: string; name: string; provider: string | null; status: number; threshold: number;
  windowMinutes: number; repeatMode: AlertRepeatMode; repeatIntervalMinutes: number | null;
  isActive: boolean; isTriggered: boolean; lastTriggeredAt: string | null; createdAt: string;
}
export interface AlertNotification {
  id: string; ruleId: string; ruleName: string; provider: string | null; status: number;
  matchCount: number; windowMinutes: number; createdAt: string; isRead: boolean; readAt: string | null;
}
