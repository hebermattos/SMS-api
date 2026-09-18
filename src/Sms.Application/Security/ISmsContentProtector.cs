namespace Sms.Application.Security;

public interface ISmsContentProtector
{
    string Protect(Guid tenantId, Guid messageId, string field, string plaintext);
    string Unprotect(Guid tenantId, Guid messageId, string field, string protectedValue);
}
