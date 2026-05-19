"""
Inicialização da base de dados.

Responsabilidades:
    1. Importar todos os modelos SQLModel para que o metadata fique registado
    2. Criar as tabelas que ainda não existam via SQLModel.metadata.create_all()
    3. Executar as migrações SQL manuais (001, 002, 003, 004, ...)
"""

from sqlmodel import SQLModel

import app.db.models  # pylint: disable=unused-import

from app.db.database import engine
from app.db.migrate import run_migrations


def init_db() -> None:
    """
    Cria as tabelas na BD caso não existam
    Em produto, isto é substituido por migrações
    """
    SQLModel.metadata.create_all(engine)
    # Executa migrações manuais simples.
    run_migrations()
