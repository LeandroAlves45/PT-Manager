"""
Configuração de base de dados e factory de sessões.

Responsabilidades:
  - Criar e configurar o engine SQLModel
  - Fornecer dependency FastAPI para injeção de sessões em handlers
  - Lidar com argumentos específicos por tipo de BD (SQLite, PostgreSQL, etc.)
"""

from typing import Generator
from sqlmodel import Session, create_engine
from app.core.config import settings


def _get_connect_args() -> dict:
    """
    Retorna argumentos de conexão específicos para cada tipo de base de dados.

    SQLite: {"check_same_thread": False} — permite múltiplas threads
    PostgreSQL/MySQL: {} — não há argumentos especiais necessários

    Returns:
        dict: Argumentos a passar no `create_engine(..., connect_args=...)`
    """
    if settings.database_url.startswith("sqlite"):
        return {"check_same_thread": False}
    return {}


# Engine global — criado uma vez no startup
engine = create_engine(
    settings.database_url,
    echo=False,
    connect_args=_get_connect_args(),
    pool_pre_ping=True,  # Valida conexão antes de usar (evita "connection dropped" em idle)
)


def get_session() -> Generator[Session, None, None]:
    """
    Dependency FastAPI que fornece uma sessão de base de dados por request.

    Cria uma nova sessão SQLModel com context manager automático (with).
    A sessão é fechada automaticamente após o handler terminar.

    Yields:
        Session: Instância pronta para queries.

    Example:
        @router.get("/users")
        async def list_users(session: Session = Depends(get_session)):
            return session.exec(select(User)).all()
    """
    with Session(engine) as session:
        yield session
