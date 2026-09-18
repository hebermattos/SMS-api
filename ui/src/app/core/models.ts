export type PortalRole = 'admin' | 'tenant';
export interface TokenResponse { access_token: string; token_type: string; }
export interface Tenant { id: string; name: string; isActive: boolean; createdAt: string; }
export interface Client { id: string; clientId: string; isActive: boolean; createdAt: string; }
export interface Administrator { id: string; username: string; isActive: boolean; createdAt: string; }
export interface IssuedSecret { clientId: string; clientSecret: string; }
export interface ProvisionedTenant { tenant_id: string; name: string; client_id: string; client_secret: string; }
export interface ProviderField { key: string; label: string; secret: boolean; required: boolean; }
export interface ProviderDefinition { name: string; accountLabel: string; secretLabel: string; fields: ProviderField[]; }
export interface ProviderConfig {
  provider: string; accountId: string; fromNumber: string; isActive: boolean; isDefault: boolean;
  hasApiSecret: boolean; settings: Record<string, string | null>; configuredSecrets: string[];
}
export interface AvailableProvider { name: string; fromNumber: string; isDefault: boolean; }
export interface Overview { name: string; outbound: number; inbound: number; delivered: number; failed: number; pending: number; providers: AvailableProvider[]; }
export interface Message {
  id: string; from: string; to: string; body: string; provider: string; providerMessageId: string | null;
  direction: number; status: number; createdAt: string; updatedAt: string | null;
}
export interface StatusHistory { id: string; messageId: string; status: number; createdAt: string; }
export interface LogEntry { id: number; timestamp: string; severity: string; category: string; message: string; traceId: string | null; spanId: string | null; }
export interface SendResult { id: string; provider: string; status: string; }
