# Data Breach Response Procedure

Se PT Manager detecta um data breach envolvendo dados de pagamento:

## Level 1: Investigation (Primeiras 6 horas)

1. Ativar incident response team
2. Isolate affected systems (se necessário)
3. Gather logs e evidence
4. Determine scope: quantos registos foram afetados?
5. Confirm se card data ou only payment method IDs foram afetados

**Checklist:**
- Stripe dashboard → Events → Review all recent activity
- Database audit logs → Search for unusual queries
- Server logs → Check for unauthorized access
- Network logs → Review firewall logs
- Document timeline of events

## Level 2: Internal Notification (24 horas)

1. Notify PT Manager leadership
2. Notify security team
3. Notify legal team (GDPR compliance)
4. Notify Stripe support
5. Document all findings

**Communication Template:**
```
Subject: URGENT: Potential Data Breach on [DATE]

Timeline:
- [TIME]: Issue detected
- [TIME]: Investigation started
- [TIME]: Scope determined

Affected Data:
- [NUMBER] customers
- [TYPE]: Payment Method IDs only (no card data)
- No CVV/PAN exposed

Actions Taken:
- Systems isolated
- Logs collected
- Stripe notified

Next Steps:
- Continue investigation
- Customer notification (24-48h)
- Post-mortem report
```

## Level 3: External Notification (24-48 horas)

**Only if card data was exposed** (PT Manager doesn't store raw card data):

1. Notify affected customers (email + SMS)
2. Notify payment processors (Stripe)
3. Notify regulators (if required by law)
4. Notify insurance company

**Customer Notification Template:**
```
Dear Customer,

On [DATE], we discovered [BRIEF DESCRIPTION of incident].

What happened:
[Explain incident in simple terms]

What data was affected:
[List specific data]

What we did:
[Actions taken to secure systems]

What you should do:
[Recommendations: monitor card, enable fraud alerts]

Questions:
Contact security@ptmanager.com
```

## Level 4: Post-Mortem (1 week)

1. Complete forensics analysis
2. Identify root cause
3. Implement fix
4. Review security controls
5. Document lessons learned
6. Create action plan to prevent recurrence

**Post-Mortem Report Should Include:**
- Timeline of events
- Root cause analysis
- Impact assessment
- Remediation actions
- Preventive measures
- Owner accountability
- Follow-up dates

## Regulatory Notifications

**If PAN/CVV exposed (PT Manager shouldn't have this):**
- Stripe (immediate)
- Acquiring bank (72 hours)
- Regional regulators (within legal timeframe)
- Credit bureaus (if 1000+ records)

**If Payment Method IDs exposed (tokenized, less critical):**
- Stripe notification
- Customer notification (out of abundance of caution)
- No regulatory notification required (since no card data)

## Prevention for Future

- Annual penetration testing
- Quarterly vulnerability scanning
- Implement WAF (Web Application Firewall)
- Implement SIEM (Security Information Event Management)
- Rotate webhook secrets annually
- Enforce 2FA on all admin accounts
- Implement rate limiting (already done)
- Implement audit logging (already done)
