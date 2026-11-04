# ADR-003: como a app nativa fala com a API

- Data: 2026-11-04
- Estado: aceite (8D-4). Merge `49bb973` na `main`, em produção no mesmo dia.
- Contexto:
  - A app Capacitor corre a SPA numa WebView com origem própria: `capacitor://localhost` no iOS e
    `https://localhost` no Android. Para a API, é outro site.
  - A web usa um cookie HttpOnly de refresh com `[RequireOrigin]` e CORS com credenciais. Na app esse
    cookie não funciona, e `https://localhost` é a origem de qualquer app Capacitor: não pode entrar
    na allowlist das origens com credenciais.
  - O 8D-2 já tinha a sessão nativa (`/api/v1/native/auth/*`, refresh token e segredo anti-CSRF no
    corpo). Faltava decidir como a app faz os pedidos HTTP.
- Decisão:
  - A app usa o `fetch` da WebView. O backend tem uma política CORS própria, `NativeApp`, para as
    duas origens fixas: **sem credenciais** e sem o cabeçalho `X-CSRF-Token`. Um
    `ICorsPolicyProvider` escolhe-a pelo cabeçalho Origin; as outras origens continuam na política
    `Frontend`.
  - `Cors:AllowedOrigins` recusa `https://localhost`, por isso as rotas de cookie
    (`[RequireOrigin]`) continuam a recusar a app.
  - As rotas de palavra-passe que a app usa (`change-password`, `password-reset/request`,
    `password-reset/complete`) passam a existir também em `/api/v1/native/auth`, com os mesmos
    handlers e limites.
  - No cliente, `isNativeApp()` decide: `credentials: 'omit'`, sem `X-CSRF-Token`, refresh pelo
    corpo com o par guardado no Keychain/Keystore.
- Alternativas:
  - **CapacitorHttp** (pedidos nativos, sem CORS nem Origin). Recusado: ignora o corpo quando o
    `fetch` recebe um `Request`, que é o que o `openapi-fetch` passa; o multipart da fotografia de
    perfil é frágil; e o comportamento não se testa no backend.
  - **Juntar `https://localhost` à allowlist atual.** Recusado: dava credenciais e passava o
    `RequireOriginFilter` a qualquer app Capacitor.
  - **`server.url` com o site remoto.** Recusado: o Capacitor não o recomenda em produção e a Apple
    recusa apps que são só um site embrulhado (4.2).
- Consequências:
  - Sem credenciais, outra app com a mesma origem só consegue o que um `curl` já consegue: rotas
    anónimas, ou rotas com Bearer para as quais precisa do token.
  - Uma rota nova que a app precise e que esteja num controller com `[RequireOrigin]` tem de ter a
    versão nativa. Os testes `NativeAppCorsTests` e `NativeAuthControllerTests` provam o desenho.
  - Detalhe e evidência: `.claude/memory/Sessions/2026-11-04-sprint8d4-app-nativa.md`.
