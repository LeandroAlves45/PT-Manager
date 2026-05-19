"""
Modelo para tokens de autenticação ativos.

Cada utilizador autenticado possui, no máximo, uma linha nesta tabela.
Ao fazer login, a linha anterior é excluída e substituída — apenas o token mais recente
por utilizador é válido em qualquer momento.

Por que armazenar hash de tokens na base de dados?

Os JWTs são sem estado por design — uma vez emitidos, são válidos até expirarem,
mesmo após reinicialização do servidor ou alteração de password. Armazenar apenas
o hash SHA-256 do token aqui oferece:

1. Logout imediato: eliminar a linha invalida o token instantaneamente
2. Revogação de sessões: alteração de password revoga todos os tokens activos
3. Segurança: apenas o hash é armazenado, nunca o JWT bruto

Design de segurança:

A coluna token_hash contém SHA-256(JWT), NUNCA o token bruto. Isto protege contra
compromisso da BD — mesmo com acesso total de leitura, um atacante não consegue
forjar tokens. O JWT bruto viaja apenas nas respostas HTTP; internamente comparamos
apenas hashes.

Ciclo de vida:
- Tokens de acesso expiram após 60 minutos
- Tokens de refresh geridos separadamente (tabela refresh_tokens)
- Scheduler elimina linhas expiradas periodicamente
"""

import uuid
from datetime import datetime

from sqlmodel import SQLModel, Field


class ActiveToken(SQLModel, table=True):
    """Tokens de sessão ativos.

    Segurança: Guarda HASH do JWT, não o JWT completo.
    - Access tokens expiram em 60 min
    - Se hacked a BD, tokens não podem ser usados diretamente
    """

    __tablename__ = "active_tokens"

    id: str = Field(default_factory=lambda: str(uuid.uuid4()), primary_key=True)

    # Foreign_key de users
    user_id: str = Field(foreign_key="users.id", index=True)

    token_hash: str = Field(unique=True, index=True)

    expires_at: datetime = Field(index=True)
