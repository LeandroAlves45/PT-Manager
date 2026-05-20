"""Service para enviar emails via Resend."""

from jinja2 import Environment, FileSystemLoader
from resend import Emails
import resend

from app.core.config import settings


class EmailService:
    """Envio de emails via Resend (noreply@ptmanageronline.pt)."""

    def __init__(self):
        resend.api_key = settings.resend_api_key
        self.emails = Emails()
        template_dir = "app/templates/email"
        self.jinja = Environment(loader=FileSystemLoader(template_dir))

    def _render_template(self, template_name: str, context: dict) -> str:
        """Renderiza template Jinja2 com context."""
        template = self.jinja.get_template(template_name)
        return template.render(context)

    def send_verification_email(
        self,
        trainer_email: str,
        trainer_name: str,
        verification_link: str,
        app_name: str = "PT Manager",
    ) -> None:
        """Envia email de verificação para trainer (P0.1).

        Args:
            trainer_email: Email do trainer
            trainer_name: Nome do trainer (para personalização)
            verification_link: URL completa com token
            app_name: Nome da aplicação

        Raises:
            Exception: Se Resend falhar
        """
        html = self._render_template(
            "email-verification.html",
            {
                "trainer_name": trainer_name,
                "verification_link": verification_link,
                "expires_in_minutes": 15,
                "app_name": app_name,
                "logo_section": self._get_logo_section(),
            },
        )

        self.emails.send(
            {
                "from": "noreply@ptmanageronline.pt",
                "to": trainer_email,
                "subject": f"Verifica o Teu Email - {app_name}",
                "html": html,
            }
        )

    def send_invite_email(
        self,
        client_email: str,
        client_name: str,
        trainer_name: str,
        invite_link: str,
        expires_in_days: int = 7,
        app_name: str = "PT Manager",
    ) -> None:
        """Envia email de convite para client.

        Args:
            client_email: Email do client
            client_name: Nome do client
            trainer_name: Nome do trainer (para personalizacao)
            invite_link: URL completa com token de convite
            expires_in_days: Dias de validade do convite
            app_name: Nome da aplicacao

        Raises:
            Exception: Se Resend falhar
        """
        html = self._render_template(
            "invite_email.html",
            {
                "client_name": client_name,
                "trainer_name": trainer_name,
                "invite_link": invite_link,
                "expires_in_days": expires_in_days,
                "app_name": app_name,
                "logo_section": self._get_logo_section(),
            },
        )

        self.emails.send(
            {
                "from": "noreply@ptmanageronline.pt",
                "to": client_email,
                "subject": f"Bem-vindo ao {app_name}",
                "html": html,
            }
        )

    def _get_logo_section(self) -> str:
        """Retorna HTML da logo (placeholder).

        Pode ser customizado com logo_url do trainer mais tarde.
        Por agora usa a logo da app (hardcoded ou settings).
        """
        logo_url = getattr(settings, "app_logo_url", "https://placeholder.com/logo.png")
        img_html = (
            '<img src="{}" alt="PT Manager" '
            'style="height: 40px; margin: 0 auto; '
            'display: block;" />'
        )
        return img_html.format(logo_url)


    @staticmethod
    def send_plain_email(
        to_email: str,
        subject: str,
        body: str,
    ) -> None:
        """Envia email plain text sem template (para notificações simples).

        Args:
            to_email: Email do destinatário
            subject: Assunto do email
            body: Corpo do email em plain text

        Raises:
            Exception: Se Resend falhar
        """
        import resend
        from resend import Emails
        resend.api_key = settings.resend_api_key
        emails = Emails()
        emails.send(
            {
                "from": "noreply@ptmanageronline.pt",
                "to": to_email,
                "subject": subject,
                "text": body,
            }
        )
