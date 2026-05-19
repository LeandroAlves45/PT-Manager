"""Schemas Pydantic para criação, atualização e leitura de clientes."""

from datetime import date
from typing import Optional

from pydantic import EmailStr, field_validator, BaseModel
from sqlmodel import SQLModel, Field


class ClientCreate(SQLModel):
    """
    Payload para criação de um novo cliente.
    aqui validamos formato; regras de negócio mais avançadas ficam em services
    """

    full_name: str = Field(min_length=1, max_length=200)
    phone: str = Field(min_length=8, max_length=20)
    email: Optional[EmailStr] = None

    birth_date: date
    sex: Optional[str] = None

    @field_validator("sex")
    @classmethod
    def normalize_sex(cls, v: Optional[str]) -> Optional[str]:
        """Normaliza valor de sexo para lowercase e mapeia variações."""
        if v is None:
            return v
        v = v.strip().lower()
        mapping = {
            "male": "male",
            "m": "male",
            "masculino": "male",
            "female": "female",
            "f": "female",
            "feminino": "female",
            "other": "other",
            "unknown": "unknown",
        }
        if v not in mapping:
            raise ValueError("Sexo deve ser: male, female, other, unknown")
        return mapping[v]

    height_cm: Optional[int] = Field(default=None, ge=80, le=260)
    objective: Optional[str] = Field(default=None, max_length=500)
    training_modality: str = Field(default="presencial", max_length=20)

    @field_validator("training_modality")
    @classmethod
    def validate_training_modality(cls, v: str) -> str:
        """Valida que training_modality é presencial ou online."""
        allowed = {"presencial", "online"}
        if v not in allowed:
            raise ValueError(
                f"training_modality deve ser: {', '.join(sorted(allowed))}"
            )
        return v

    notes: Optional[str] = None
    emergency_contact_name: Optional[str] = None
    emergency_contact_phone: Optional[str] = None


class ClientUpdate(SQLModel):
    """Payload para atualização de um cliente. Todos os campos são opcionais."""

    full_name: Optional[str] = Field(default=None, min_length=1, max_length=200)
    phone: Optional[str] = Field(default=None, min_length=8, max_length=20)
    email: Optional[EmailStr] = None

    birth_date: Optional[date] = None
    sex: Optional[str] = None

    @field_validator("sex")
    @classmethod
    def validate_sex(cls, v: Optional[str]) -> Optional[str]:
        """Valida que sex é male, female, other ou unknown."""
        if v is None:
            return v
        v = v.strip().lower()
        mapping = {
            "male": "male",
            "m": "male",
            "masculino": "male",
            "female": "female",
            "f": "female",
        }
        if v not in mapping:
            raise ValueError("Sexo deve ser: male, female, other, unknown")
        return mapping[v]

    height_cm: Optional[int] = Field(default=None, ge=80, le=260)
    objective: Optional[str] = Field(default=None, max_length=500)
    training_modality: Optional[str] = None

    @field_validator("training_modality")
    @classmethod
    def validate_training_modality(cls, v: Optional[str]) -> Optional[str]:
        """Valida que training_modality é presencial, online ou None."""
        if v is None:
            return v
        allowed = {"presencial", "online"}
        if v not in allowed:
            raise ValueError(
                f"training_modality deve ser: {', '.join(sorted(allowed))}"
            )
        return v

    next_assessment_date: Optional[date] = None

    notes: Optional[str] = None

    emergency_contact_name: Optional[str] = None
    emergency_contact_phone: Optional[str] = None


class ClientRead(SQLModel):
    """Schema para leitura de dados do cliente em respostas de API."""

    id: str
    full_name: str
    phone: str
    email: Optional[str]

    birth_date: date
    sex: Optional[str]
    height_cm: Optional[int]
    objective: Optional[str] = None
    training_modality: str
    next_assessment_date: Optional[date] = None
    notes: Optional[str] = None
    emergency_contact_name: Optional[str] = None
    emergency_contact_phone: Optional[str] = None
    status: str
    created_at: date
    updated_at: date


class ActivePackInfo(BaseModel):
    """Informação sobre o pacote de sessões ativo do cliente."""

    client_pack_id: str
    pack_type_id: str
    pack_type_name: str
    sessions_total: int
    sessions_used: int
    sessions_remaining: int


class ClientReadWithPack(ClientRead):
    """Schema ClientRead com informação do pacote ativo."""

    active_pack: Optional[ActivePackInfo] = None
