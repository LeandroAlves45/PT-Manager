from typing import Optional
from datetime import date
from sqlmodel import SQLModel

class ClientPackPurchase(SQLModel):
    """
    Payload para comprar um pack para um cliente.
    """

    pack_type_id: str
    purchase_at: Optional[date] = None


class ClientPackRead(SQLModel):
    """
    Resposta pública de um pack comprado por um cliente.
    """

    id: str
    client_id: str
    client_name: Optional[str] = None
    pack_type_id: str
    purchase_at: date
    sessions_total_snapshot: int
    sessions_used: int
    cancelled_at: Optional[date] = None
    archived_at: Optional[date] = None
    created_at: date
    updated_at: date

