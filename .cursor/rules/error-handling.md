---
paths:
  - "backend/src/Api/**"
  - "backend/src/Application/**"
---

# Error Handling — PT Manager

- Usar `Result` e `Result<T>` na `Application` para falhas esperadas; não lançar exceções para controlo de fluxo de negócio.
- Converter erros para `Problem Details` na fronteira HTTP (`Api`), com o código de estado correto (400 validação, 401 auth, 403 forbidden, 404 not found, 409 conflict, 500 inesperado).
- Nunca engolir erros silenciosamente. Registar com contexto sobre a operação que falhou.
- Nunca expor stack traces, caminhos internos ou erros de base de dados em bruto nas respostas de produção.
- Manter compatibilidade do campo `detail` nas respostas de erro (contrato HTTP existente).
- Incluir contexto de correlação nos logs de erro quando disponível.
- Controllers permanecem finos: mapeiam `Result`/`Result<T>` da `Application` para respostas HTTP, sem lógica de negócio.
