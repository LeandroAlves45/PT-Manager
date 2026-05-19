"""Repositorio para Active Tokens com hash seguro."""

from datetime import datetime
from typing import Optional
from sqlmodel import Session, select
from app.db.models import ActiveToken
from app.utils.db_errors import commit_or_rollback

class ActiveTokenRepository:
    """Abstrai operações com Active Tokens. Guarda HASH e não token completo."""

    @staticmethod
    def get_by_user_id(user_id: str, session: Session) -> Optional[ActiveToken]:
        """Busca token ativo do user."""

        return session.exec(
          select(ActiveToken).where(ActiveToken.user_id == user_id)
        ).first()

    @staticmethod
    def get_by_user_id_and_token_hash(
      user_id: str,
      token_hash: str,
      session: Session,
    ) -> Optional[ActiveToken]:
        """Busca token específico pelo hash (validação segura)."""

        return session.exec(
          select(ActiveToken)
          .where(ActiveToken.user_id == user_id)
          .where(ActiveToken.token_hash == token_hash)
        ).first()

    @staticmethod
    def save_or_replace(
      user_id: str,
      token_hash: str,
      expires_at: datetime,
      session: Session,
    ) -> None:
        """
        Substitui token anterior e salva novo.
        Usado em login e refresh -> reutiliza código.
        
        Segurança: Guarda HASH do token, não o token completo.
        """

        # 1. Remover token anterior se existe
        existing = ActiveTokenRepository.get_by_user_id(user_id, session)
        if existing:
            session.delete(existing)
            session.flush()

        # 2. Inserir novo token (apenas hash)
        new_token = ActiveToken(
          user_id=user_id,
          token_hash=token_hash,
          expires_at=expires_at,
        )
        session.add(new_token)
        commit_or_rollback(session)

    @staticmethod
    def revoke_by_user_id(
      user_id: str,
      session: Session,
    ) -> None:
        """Revoga token do user(logout)."""

        token = ActiveTokenRepository.get_by_user_id(user_id, session)
        if token:
            session.delete(token)
            commit_or_rollback(session)
