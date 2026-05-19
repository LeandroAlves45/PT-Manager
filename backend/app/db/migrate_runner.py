"""
migrate_runner.py — Script standalone para execução de migrations.
 
Por que existe este ficheiro separado?
--------------------------------------
Anteriormente, run_migrations() era chamado dentro do lifespan() do FastAPI,
ou seja, corria no mesmo processo que serve pedidos HTTP. Isto causava dois problemas:
 
1. DOWNTIME: Durante um deploy, o Render arranca o novo container enquanto o antigo
   ainda serve tráfego. Se a migration demora, pedidos chegam contra um schema incompleto.
 
2. RACE CONDITION em scale-out: Com 2+ instâncias a arrancar ao mesmo tempo,
   ambas correriam run_migrations() simultaneamente. A tabela schema_migrations
   protege contra execução dupla, mas existe uma janela de tempo entre o
   SELECT (ler migrations aplicadas) e o INSERT (marcar como aplicada).

Uso:
   python -m app.db.migrate_runner          # via módulo Python
   python app/db/migrate_runner.py          # directo
"""

import sys
import logging

# Configura logging mínimo para o runner, sem setup_logging() completo. Utilitário standalone
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [MIGRATE] %(levelname)s: %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger(__name__)

def main() -> int:
    """
    Ponto de entrada do runner de migrations.
 
    Retorna:
        0 — todas as migrations aplicadas com sucesso (ou já estavam aplicadas)
        1 — erro durante a execução (deploy deve ser cancelado)
    """

    logger.info("A iniciar o runner de migrations...")

    try:
        # Importa apenas quando necessário, evita erros de import
        from app.db.migrate import run_migrations

        run_migrations()

        logger.info("Migrations aplicadas com sucesso.")
        return 0
    
    except Exception as e:
        # Qualquer erro aqui deve bloquear o deploy 
        # (preferível cancelar do que arrancar a app com schema incompleto ou inconsistente)
        logger.error(f"Falha nas migrations - deploy cancelado: {exc}", exc_info=True)
        return 1
    
if __name__ == "__main__":
    # Quando chamado diretamente: python app/db/migrate_runner.py
    sys.exit(main())