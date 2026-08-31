using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshSessionCsrf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WHY: o Plan §2.5 exige que a migration CSRF revogue todas as refresh
            // sessions existentes e force novo login. Sessões criadas antes desta
            // coluna não têm segredo anti-CSRF associado, pelo que nunca poderiam
            // completar um refresh; deixá-las marcadas como ativas seria mentir sobre
            // o estado da base de dados.
            //
            // O WHERE é obrigatório: sem ele, o UPDATE reescreveria a data de revogação
            // de sessões já revogadas e destruiria informação de auditoria.
            migrationBuilder.Sql(
                "UPDATE refresh_tokens SET revoked_at = now() WHERE revoked_at IS NULL;");

            migrationBuilder.AddColumn<string>(
                name: "csrf_token_hash",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // WHY: o default só existe para permitir preencher as linhas existentes
            // durante o ALTER. Mantê-lo permitiria inserir uma sessão sem CSRF por
            // omissão, contornando a invariante que o construtor da entidade impõe.
            migrationBuilder.Sql(
                "ALTER TABLE refresh_tokens ALTER COLUMN csrf_token_hash DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WHY: o Down remove a coluna e devolve o schema anterior. As sessões
            // revogadas pelo Up NÃO são restauradas, e não podem ser: a revogação é
            // uma decisão de segurança irreversível por desenho, e o valor original de
            // revoked_at não foi guardado em lado nenhum. Um rollback devolve o schema,
            // não as sessões — e isso está aqui escrito para que ninguém conte com o
            // contrário.
            migrationBuilder.DropColumn(
                name: "csrf_token_hash",
                table: "refresh_tokens");
        }
    }
}
