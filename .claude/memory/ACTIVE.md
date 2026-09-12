# Estado ativo: Sprint 5C finalizada; User Secrets dos providers pendentes

Atualizado: 2026-09-12

## Estado em uma linha

A Fase 5B está fechada (falta configurar secrets Stripe). A implementação real da Fase
5C foi revista, corrigida e validada em 2026-09-12. As cinco suites passaram com 2348
testes, zero falhas e um teste manual ignorado. A migration
`20260912145051_AddManagedImageAssets` passou o ciclo Up, Down e reaplicação em
PostgreSQL 17 descartável e foi aplicada à base local persistente. A consulta direta a
`__EFMigrationsHistory` confirmou-a como a migration mais recente, com EF Core 10.0.10.
Os três preflights operacionais devolveram zero. Resta apenas fornecer os User Secrets
de Cloudinary e Vision antes de ativar os providers.

## Decisões da 5C

1. Moderação Google Vision SafeSearch, síncrona e fail-closed, autenticada por service
   account (`Google.Apis.Auth`). Add-ons Cloudinary rejeitados por serem assíncronos.
2. Cloudinary por HttpClient tipado, sem SDK.
3. SkiaSharp 4.152.0 + `SkiaSharp.NativeAssets.Linux.NoDependencies`; saída WebP.
4. Só o avatar é moderado; o logo não.
5. Imagem runtime Debian, nunca Alpine (restrição para o Dockerfile por escrever).
6. Sem `[RequireOrigin]` nos uploads: autenticação Bearer.
7. `IMediaStorage.DeleteAsync` recebe o `trainerId` e recusa identificadores de outro tenant.
8. Migration `20260912145051_AddManagedImageAssets` com preflights manuais em Up e Down.

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/memory/Sessions/2026-09-12-sprint5c-review-validacao.md`.
3. `docs/backend-files/sprint_5/sprint_5C/00_desenho_aprovado_indice_dependencias_gates.md`.
4. `docs/backend-files/sprint_5/sprint_5C/13_qa_da_fase.md`.
5. `docs/backend-files/sprint_5/sprint_5C/14_rastreabilidade_revisao_quality_gates.md`.
6. `docs/backend-files/sprint_5/sprint_5C/15_revisao_validacao_implementacao.md`.

## Gates abertos

`QG5C-ROLL-001`, `QG5C-TEST-003` e `QG5C-MIG-001` estão fechados.
`QG5C-PROVIDER-001` aguarda apenas os User Secrets e a ativação dos providers. Da 5B continuam abertos
`QG5B-STRIPE-001` e `QG5B-DEPLOY-001`.

## Passo imediato

Configurar `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret` e
`Vision:ServiceAccountJson` em User Secrets. Só depois ativar `Cloudinary:Enabled` e
`Vision:Enabled`; a validação de arranque deve permanecer fail-closed.
