"""Centraliza logging estruturado de erros para debug."""

import traceback
from datetime import datetime, timezone
from enum import Enum
from typing import Optional

from pydantic import BaseModel


class ErrorCategory(str, Enum):
    """Categorias de erros."""
    EMAIL_SERVICE = "email_service"
    DATABASE = "database"
    VALIDATION = "validation"
    UNKNOWN = "unknown"


class ErrorLog(BaseModel):
    """Registo estruturado do erro."""
    timestamp: datetime
    notification_id: str
    category: ErrorCategory
    error_type: str
    error_message: str
    retry_count: int
    max_retries: int
    stacktrace: Optional[str] = None


class ErrorTracker:
    """Centraliza logging estruturado de erros para debug."""

    @staticmethod
    def categorize_error(exception: Exception) -> ErrorCategory:
        """Identifica o tipo de erro."""
        exc_type = type(exception).__name__

        if "SMTP" in exc_type or "Email" in exc_type:
            return ErrorCategory.EMAIL_SERVICE
        elif "SQL" in exc_type or "Database" in exc_type:
            return ErrorCategory.DATABASE
        elif "ValueError" in exc_type:
            return ErrorCategory.VALIDATION
        return ErrorCategory.UNKNOWN

    @staticmethod
    def log_error(
        notification_id: str,
        exception: Exception,
        retry_count: int,
        max_retries: int,
    ) -> ErrorLog:
        """Cria e retorna um registo estruturado do erro."""
        return ErrorLog(
            timestamp=datetime.now(timezone.utc),
            notification_id=notification_id,
            category=ErrorTracker.categorize_error(exception),
            error_type=type(exception).__name__,
            error_message=str(exception)[:400],
            retry_count=retry_count,
            max_retries=max_retries,
            stacktrace=traceback.format_exc(),
        )
