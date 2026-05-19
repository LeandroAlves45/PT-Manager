"""Repository para Refresh Tokens — segurança de sessão."""

import logging
from datetime import datetime
from typing import Optional
from sqlmodel import Session, select
from app.db.models import RefreshToken
from app.utils.db_errors import commit_or_rollback
from app.utils.time import utc_now_datetime

logger = logging.getLogger(__name__)

class RefreshTokenRepository:
    """Abstrai operações com Refresh Tokens."""

    @staticmethod
    def get_by_hash(
        token_hash: str,
        session: Session,
        device_hint: str = None,
        ):
        """
        Busca refresh token por hash e valida device fingerprint se fornecido.
        
        Se device_hint for fornecido: valida que o User-Agent é o mesmo (protege contra token theft)
        Se não fornecido: apenas valida o hash (comportamento legado)
        """

        query = session.exec(
            select(RefreshToken)
            .where(RefreshToken.token_hash == token_hash)
            .where(RefreshToken.revoked_at.is_(None))
        ).first()

        if query is None:
            return None

        # Validar device fingerprint se fornecido
        if device_hint and query.device_hint != device_hint:
            logger.warning(
                "[DEVICE_HIJACKING] Token válido mas device mudou: user_id=%s, stored=%s, "
                "current=%s",
                query.user_id,
                query.device_hint [:50],
                device_hint[:50],
            )
            query.revoked_at = utc_now_datetime()
            session.add(query)
            return None

        return query

    @staticmethod
    def save(
        user_id: str,
        token_hash: str,
        expires_at: datetime,
        device_hint: Optional[str],
        session: Session,
    ) -> RefreshToken:
        """Guarda novo Refresh Token."""

        refresh_token = RefreshToken(
            user_id=user_id,
            token_hash=token_hash,
            expires_at=expires_at,
            device_hint=device_hint[:100] if device_hint else None,
        )

        session.add(refresh_token)
        commit_or_rollback(session)
        return refresh_token

    @staticmethod
    def revoke_by_hash(token_hash: str, session: Session) -> None:
        """Revoga um Refresh Token específico (rotational security)."""

        token = RefreshTokenRepository.get_by_hash(token_hash, session)
        if token:
            token.revoked_at = utc_now_datetime()
            session.add(token)
            commit_or_rollback(session)

    @staticmethod
    def revoke_all_by_user_id(user_id: str, session: Session) -> None:
        """Revoga todos os Refresh Tokens de um utilizador (logout completo)."""

        tokens = session.exec(
            select(RefreshToken)
            .where(RefreshToken.user_id == user_id)
            .where(RefreshToken.revoked_at.is_(None))
        ).all()

        for token in tokens:
            token.revoked_at = utc_now_datetime()
            session.add(token)

        if tokens:
            commit_or_rollback(session)
