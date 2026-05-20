"""
Modelo de base de dados para utilizadores autenticados.

Um User representa qualquer pessoa que faz login no sistema.
O campo role determina o que cada utilizador pode fazer.
"""

import uuid
from typing import Optional
from datetime import datetime

from sqlmodel import SQLModel, Field
from app.utils.time import utc_now


class User(SQLModel, table=True):
    """
    Representa um utilizador autenticado no sistema.

    Roles disponíveis:
    - "superuser": Tem acesso total a todas as funcionalidades do sistema,
    incluindo a gestão de outros utilizadores e a visualização de todos os dados.
    - "trainer": Pode criar e gerir os seus próprios treinos,
    mas não tem acesso aos treinos de outros utilizadores.
    - "client": acesso aos apenas aos seus dados (read-only na maioria dos casos).

    Relação dos clientes:
     - Um User com role= "client" tem um client_id associado, registo da tabela Client)
     - Um User com role="trainer" não tem client_id
     - Esta separação é importante para garantir que os clientes só acedem aos seus próprios dados e
      para facilitar a gestão dos treinos pelos Personal Trainers.
    """

    __tablename__ = "users"

    id: str = Field(default_factory=lambda: str(uuid.uuid4()), primary_key=True)

    # Email é o identificador de login, único no sistema
    email: str = Field(index=True, unique=True)

    # Password é armazenada como hash para garantir a segurança dos dados dos utilizadores.
    # Nunca deve ser armazenada em texto plano.
    hashed_password: str = Field()

    # Role do utilizador:
    #   "superuser" — acesso total, sem restrições de ownership ou subscrição
    #   "trainer"   — acesso aos seus próprios dados, sujeito a subscrição activa
    #   "client"    — acesso read-only aos seus próprios dados
    role: str = Field(default="client")

    client_id: Optional[str] = Field(default=None, foreign_key="clients.id", index=True)

    full_name: str = Field()

    # URL do logo do Personal Trainer
    # (uso do Cloudinary para armazenar imagens dos Personal Trainers)
    logo_url: Optional[str] = Field(default=None, max_length=500)

    # Conta ativa ou não, para permitir desativar contas sem as eliminar da base de dados.
    is_active: bool = Field(default=True)

    # Campo para a isenção de billing, caso True passa imediatamente pelo Stripe
    is_exempt_from_billing: bool = Field(default=False)

    # Campos de convite para permitir que os Personal Trainers convidem clientes para o sistema,
    # associando-os diretamente ao Personal Trainer.
    invite_token_hash: Optional[str] = Field(default=None, max_length=64)
    invite_token_expires_at: Optional[datetime] = Field(default=None)

    # Campos para tokens de verificação de email
    email_verification_token_hash: Optional[str] = Field(
        default=None, max_length=64, index=True
    )
    email_verification_token_expires_at: Optional[datetime] = Field(default=None)

    # Campo para verificar se o email do Personal Trainer foi verificado
    email_verified: bool = Field(default=False)
    email_verified_at: Optional[datetime] = Field(default=None)

    created_at: datetime = Field(default_factory=utc_now)
    updated_at: datetime = Field(default_factory=utc_now)
