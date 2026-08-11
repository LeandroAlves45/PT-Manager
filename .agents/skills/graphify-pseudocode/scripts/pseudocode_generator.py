#!/usr/bin/env python3
"""Generate a deterministic per-file pseudocode blueprint for PT Manager."""

from __future__ import annotations

import argparse
from pathlib import Path


VALID_STATES = ("existing", "incomplete", "to create")


def build_blueprint(
    feature: str,
    layer: str,
    file_path: str,
    state: str,
    responsibility: str,
) -> str:
    """Build one continuous blueprint without splitting docs from behavior."""
    namespace = derive_namespace(file_path)

    return f"""# {feature}

## Ficheiro alvo

`{file_path}`

Estado atual: {state}.

Adequação: camada {layer}. {responsibility}

```text
using [DEPENDÊNCIAS REAIS INSPECIONADAS NO PROJETO]

namespace {namespace}

/// <summary>
/// [RESPONSABILIDADE PÚBLICA COMPLETA EM PORTUGUÊS DE PORTUGAL]
/// </summary>
PUBLIC SEALED CLASS {feature}
    PRIVATE READONLY FIELD _dependency TYPE [PORTA REAL]

    /// <summary>Inicializa apenas as dependências exigidas pelo caso de uso.</summary>
    PUBLIC CONSTRUCTOR([PORTA REAL] dependency)
        THROW ArgumentNullException IF dependency IS NULL
        _dependency = dependency
    END CONSTRUCTOR

    /// <summary>
    /// [COMPORTAMENTO, INPUT, OUTPUT E FALHAS ESPERADAS]
    /// </summary>
    PUBLIC ASYNC METHOD HandleAsync(
        [COMMAND OU QUERY REAL] request,
        CancellationToken cancellationToken)
        RETURNS Task<Result<[DTO REAL]>>

        VALIDATE request ASYNCHRONOUSLY WITH cancellationToken
        IF validation fails
            RETURN Result.Failure WITH stable field errors
        END IF

        // WHY: o tenant nasce do contexto autenticado e nunca do payload.
        REQUIRE trainer id FROM ITenantContext
        IF tenant is unavailable
            RETURN tenant failure
        END IF

        EXECUTE [LÓGICA COMPLETA DO FICHEIRO, SEM REMETER CORPOS PARA OUTRA SECÇÃO]
        PROPAGATE cancellationToken TO EVERY I/O CALL
        MAP every expected outcome TO Result success or stable failure
        THROW only for impossible or unknown technical states
    END METHOD
END CLASS
```

## Nota de mentor

Explicar as decisões não óbvias deste ficheiro, sem repetir linha a linha o bloco.

## Validações

1. Confirmar o caminho, namespace e estado contra o repositório.
2. Confirmar que o bloco contém todos os membros e branches necessários.
3. Confirmar Result, tenant, transação, concorrência e CancellationToken aplicáveis.
4. Confirmar testes dos cenários críticos, sem meta global de cobertura.
"""


def derive_namespace(file_path: str) -> str:
    """Derive a likely namespace from a standard backend/src target path."""
    normalized = file_path.replace("\\", "/")
    marker = "backend/src/"
    if marker not in normalized:
        return "[NAMESPACE REAL]"

    relative = normalized.split(marker, maxsplit=1)[1]
    parts = relative.split("/")[:-1]
    return ".".join(parts) if parts else "[NAMESPACE REAL]"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate one continuous PT Manager pseudocode blueprint."
    )
    parser.add_argument("--feature", required=True)
    parser.add_argument("--layer", required=True)
    parser.add_argument("--file-path", required=True)
    parser.add_argument("--state", choices=VALID_STATES, required=True)
    parser.add_argument(
        "--responsibility",
        default="Definir uma única responsabilidade coerente com Clean Architecture.",
    )
    parser.add_argument("--output", type=Path)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    blueprint = build_blueprint(
        feature=args.feature,
        layer=args.layer,
        file_path=args.file_path,
        state=args.state,
        responsibility=args.responsibility,
    )

    if args.output is None:
        print(blueprint, end="")
        return

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(blueprint, encoding="utf-8")


if __name__ == "__main__":
    main()
