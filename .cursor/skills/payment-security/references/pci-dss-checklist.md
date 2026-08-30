# PCI DSS Level 3 Compliance Checklist

PT Manager é SaaS e-commerce Level 3. Requisitos essenciais:

## 1. Network Security
- HTTPS em tudo (TLS 1.2+)
- Firewall configurado
- No default credentials
- Restrict inbound/outbound traffic

## 2. Cardholder Data Protection
- Nunca armazenar PAN completo
- Nunca armazenar CVV
- Nunca armazenar magnetic stripe data
- OK: Stripe Payment Method ID (tokenizado), last 4 digits, brand, exp date

## 3. Malware Protection
- Anti-malware software updated
- Regular scans
- Restrict file uploads

## 4. Access Control
- Unique user IDs (no shared accounts)
- Strong passwords (12+ chars, complexity)
- Restrict access by business need
- 2FA para admin/Stripe dashboard

## 5. Testing & Monitoring
- Penetration testing annual
- Vulnerability scanning quarterly
- Security patching timely
- Audit logs (1 year retention)

## 6. Data Retention
- Completed payments: 1 year (invoice trail)
- Audit logs: 1 year (compliance)
- Pending intents: 30 days max
- After 1 year: anonymize (remove personal IDs)

## 7. Incident Response
- 4-level escalation procedure
- Investigation → Internal notification → External notification → Post-mortem
- Forensics documentation
- Law enforcement notification (if required)

## 8. Third-party Security
- Stripe handles card data (PCI Level 1)
- We handle payment intents + audit logs only
- Never receive raw card data

## PT Manager Status
✓ HTTPS configured
✓ Stripe Payment Intents (no raw card handling)
✓ Webhook signature verification
✓ Audit logging middleware
✓ Rate limiting (prevent brute force)
✓ Multi-tenant isolation (ITenantContext)
✓ Data retention policy
✓ Error handling (no sensitive data in logs)

## Remaining
- Penetration testing (annual)
- Vulnerability assessment (quarterly)
- 2FA Stripe dashboard access
