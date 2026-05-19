"""
Schemas Pydantic para o fluxo de convite de clientes.
 
InviteGenerateResponse  — resposta ao trainer com o link de convite
InviteValidateResponse  — resposta pública com o nome do cliente (sem dados sensíveis)
InviteSetPassword       — payload do cliente para definir a sua password
InviteLoginResponse     — resposta com JWT após definição de password bem-sucedida
"""

from pydantic import BaseModel, Field
from app.core.config import settings

class InviteGenerateResponse(BaseModel):
    """
    Resposta ao trainer após geração do link de convite.
    O trainer copia o invite_link e envia ao cliente via WhatsApp/SMS.
    O token raw nunca é exposto separadamente — está embutido no link.
    """
    invite_link: str
    expires_in_days: int = settings.invite_expiry_days

class InviteValidateResponse(BaseModel):
    """
    Resposta pública ao carregar a página /invite/:token.
    Não inclui dados sensíveis — apenas confirma que o token é válido
    e devolve o nome do cliente para personalizar a página.
    """
    valid: bool
    client_name: str =""
    message: str =""

class InviteSetPassword(BaseModel):
    """
    Payload enviado pelo cliente ao submeter o formulário de definição de password.
    """
    new_password: str = Field(..., min_length=8, max_length=128)

class InviteLoginResponse(BaseModel):
    """
    Resposta após definição de password bem-sucedida.
    Inclui o JWT para que o cliente seja automaticamente autenticado
    e redirecionado para o dashboard sem precisar fazer login separado.
    """
    access_token: str
    token_type: str = "bearer"
    role: str
    user_id: str
    full_name: str
